using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Skyline.DataMiner.Scripting;

/// <summary>
/// DataMiner QAction Class: Process Raw Values.
/// Scales raw SNMP values, decodes the alarm code bitmask and the system time.
/// </summary>
public static class QAction
{
    private const int SystemTimeRawPid = Parameter.systemtimeraw;
    private const int AlarmCodePid = Parameter.alarmcode;

    private const int InverterStatusPid = Parameter.inverterstatus;
    private const int SystemTimePid = Parameter.systemtime;
    private const int FanStatusPid = Parameter.fanstatus;
    private const int SoundStatusPid = Parameter.alarmsoundstatus;
    private const int ActiveAlarmSummaryPid = Parameter.activealarmsummary;
    private const int ActiveAlarmCountPid = Parameter.activealarmcount;
    private const int AlarmStatusTablePid = Parameter.Alarmstatus.tablePid;
    private const int SyncSystemTimePid = Parameter.Write.syncsystemtime;
    private const int SystemTimeRawWritePid = Parameter.Write.systemtimeraw;

    /// <summary>Alarm Code bit definitions (MIB alarmCode): description, text for 0, text for 1.</summary>
    private static readonly string[][] AlarmBitDefinitions =
    {
        new[] { "Inverter Mode", "Inverter", "Bypass" },
        new[] { "Fan", "Off", "On" },
        new[] { "Sound", "Disabled", "Enabled" },
        new[] { "Inverter Fault", "Normal", "Fault" },
        new[] { "Output Shorted", "Normal", "Shorted" },
        new[] { "Overload 120%", "Normal", "Overload" },
        new[] { "Overload 150%", "Normal", "Overload" },
        new[] { "Overload Power Off", "Normal", "Power Off" },
        new[] { "Over Temperature Power Off", "Normal", "Power Off" },
        new[] { "Reserved", "Inactive", "Active" },
        new[] { "No Battery (DC Input Missing)", "Normal", "No Battery" },
        new[] { "Battery Low (DC Input Low)", "Normal", "Battery Low" },
        new[] { "Battery High (DC Input High)", "Normal", "Battery High" },
        new[] { "AC Loss", "Normal", "AC Loss" },
        new[] { "AC Low", "Normal", "AC Low" },
        new[] { "AC High", "Normal", "AC High" },
    };

    /// <summary>Raw PID mapped to display PID, factor and whether the raw value is a signed 16-bit value.</summary>
    private static readonly Dictionary<int, Tuple<int, double, bool>> ScaledParameters = new Dictionary<int, Tuple<int, double, bool>>
    {
        { Parameter.dcvoltageraw, Tuple.Create(Parameter.dcvoltage, 0.1, false) },
        { Parameter.acvoltageraw, Tuple.Create(Parameter.acvoltage, 0.1, false) },
        { Parameter.acfrequencyraw, Tuple.Create(Parameter.acfrequency, 0.1, false) },
        { Parameter.workfrequencyraw, Tuple.Create(Parameter.workfrequency, 0.1, false) },
        { Parameter.outputvoltageraw, Tuple.Create(Parameter.outputvoltage, 0.1, false) },
        { Parameter.outputcurrentraw, Tuple.Create(Parameter.outputcurrent, 0.1, false) },
        { Parameter.outputpowerraw, Tuple.Create(Parameter.outputpower, 1.0, false) },
        { Parameter.loadratioraw, Tuple.Create(Parameter.loadratio, 0.1, false) },
        { Parameter.internaltemperatureraw, Tuple.Create(Parameter.internaltemperature, 0.1, true) },
        { Parameter.ambienttemperatureraw, Tuple.Create(Parameter.ambienttemperature, 0.1, true) },
    };

    /// <summary>Alarm names for bits B3-B15 (B9 reserved, excluded from summary).</summary>
    private static readonly Dictionary<int, string> AlarmBitNames = new Dictionary<int, string>
    {
        { 3, "Inverter Fault" },
        { 4, "Output Short Circuit" },
        { 5, "Overload 120%" },
        { 6, "Overload 150%" },
        { 7, "Overload Shutdown" },
        { 8, "Overtemperature Shutdown" },
        { 10, "DC Input Missing" },
        { 11, "DC Input Low" },
        { 12, "DC Input High" },
        { 13, "AC Input Missing" },
        { 14, "AC Input Low" },
        { 15, "AC Input High" },
    };

    /// <summary>
    /// The QAction entry point.
    /// </summary>
    /// <param name="protocol">Link with SLProtocol process.</param>
    public static void Run(SLProtocol protocol)
    {
        try
        {
            int trigger = protocol.GetTriggerParameter();
            object value = protocol.GetParameter(trigger);

            if (trigger == SyncSystemTimePid)
            {
                protocol.SetParameter(SystemTimeRawWritePid, EncodeDateAndTime(DateTime.Now));
            }
            else if (trigger == AlarmCodePid)
            {
                ProcessAlarmCode(protocol, value);
            }
            else if (trigger == SystemTimeRawPid)
            {
                protocol.SetParameter(SystemTimePid, DecodeDateAndTime(Convert.ToString(value, CultureInfo.InvariantCulture)));
            }
            else if (ScaledParameters.TryGetValue(trigger, out var scaling))
            {
                ProcessScaledValue(protocol, value, scaling);
            }
        }
        catch (Exception ex)
        {
            protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
        }
    }

    private static void ProcessScaledValue(SLProtocol protocol, object value, Tuple<int, double, bool> scaling)
    {
        if (!TryGetNumber(value, out double raw))
        {
            return;
        }

        if (scaling.Item3 && raw > short.MaxValue)
        {
            raw -= 65536;
        }

        protocol.SetParameter(scaling.Item1, Math.Round(raw * scaling.Item2, 1));
    }

    private static void ProcessAlarmCode(SLProtocol protocol, object value)
    {
        if (!TryGetNumber(value, out double raw))
        {
            return;
        }

        int alarmCode = (int)raw & 0xFFFF;

        var rows = new List<object[]>();
        for (int bit = 0; bit < 16; bit++)
        {
            int state = GetBit(alarmCode, bit);
            rows.Add(new object[]
            {
                String.Format(CultureInfo.InvariantCulture, "B{0:00}", bit),
                String.Format(CultureInfo.InvariantCulture, "0x{0:X4}", 1 << bit),
                AlarmBitDefinitions[bit][0],
                state,
                AlarmBitDefinitions[bit][1 + state],
            });
        }

        protocol.FillArray(AlarmStatusTablePid, rows, NotifyProtocol.SaveOption.Full);

        var pids = new List<int>();
        var values = new List<object>();

        var activeAlarms = AlarmBitNames.Where(a => GetBit(alarmCode, a.Key) == 1).Select(a => a.Value).ToList();

        pids.AddRange(new[] { InverterStatusPid, FanStatusPid, SoundStatusPid, ActiveAlarmSummaryPid, ActiveAlarmCountPid });
        values.AddRange(new object[]
        {
            GetBit(alarmCode, 0),
            GetBit(alarmCode, 1),
            GetBit(alarmCode, 2),
            activeAlarms.Count > 0 ? String.Join(", ", activeAlarms) : "No Active Alarms",
            activeAlarms.Count,
        });

        protocol.SetParameters(pids.ToArray(), values.ToArray());
    }

    /// <summary>
    /// Encodes a time as an SNMPv2 DateAndTime (8 bytes) hexadecimal string.
    /// </summary>
    /// <param name="time">Time to encode.</param>
    /// <returns>Hexadecimal string, e.g. 07EA0A1610012600.</returns>
    public static string EncodeDateAndTime(DateTime time)
    {
        return String.Format(
            CultureInfo.InvariantCulture,
            "{0:X4}{1:X2}{2:X2}{3:X2}{4:X2}{5:X2}{6:X2}",
            time.Year,
            time.Month,
            time.Day,
            time.Hour,
            time.Minute,
            time.Second,
            time.Millisecond / 100);
    }

    private static int GetBit(int value, int bit)
    {
        return (value >> bit) & 1;
    }

    private static bool TryGetNumber(object value, out double result)
    {
        result = 0;
        return value != null && Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Decodes an SNMPv2 DateAndTime (year 2 bytes, month, day, hour, minutes, seconds, deci-seconds).
    /// </summary>
    /// <param name="hex">Hexadecimal representation of the octet string (separators allowed).</param>
    /// <returns>Formatted date and time, or "N/A" when the value cannot be decoded.</returns>
    public static string DecodeDateAndTime(string hex)
    {
        if (String.IsNullOrEmpty(hex))
        {
            return "N/A";
        }

        var clean = new StringBuilder();
        foreach (char c in hex)
        {
            if (Uri.IsHexDigit(c))
            {
                clean.Append(c);
            }
        }

        if (clean.Length < 14)
        {
            return "N/A";
        }

        string s = clean.ToString();
        int Byte(int index) => Int32.Parse(s.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        int year = (Byte(0) << 8) | Byte(1);
        int month = Byte(2);
        int day = Byte(3);
        int hour = Byte(4);
        int minute = Byte(5);
        int second = Byte(6);

        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59 || second > 59)
        {
            return "N/A";
        }

        return new DateTime(year, month, day, hour, minute, second).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
