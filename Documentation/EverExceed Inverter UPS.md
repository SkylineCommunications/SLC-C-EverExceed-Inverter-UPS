# EverExceed Inverter UPS – Connector Documentation

## About

SNMP v2c connector to monitor EverExceed (An Power) inverter/UPS devices through the enterprise MIB `AN-POWER-INVERTER-MIB` (`1.3.6.1.4.1.18001.1`). Requested by Megacable.

- Device identification: `sysObjectID = 1.3.6.1.4.1.18001.1`
- Connection: SNMP v2c, port 161, get community configured on the element.
- Read-only: no SNMP sets are performed (writable OIDs are monitored only).

## Polling

| Timer | Interval | Content |
|---|---|---|
| Fast | 10 s | Alarm Code (`.2.11.0`) |
| Medium | 30 s | Electrical values, temperatures, supply priority, power state, timer OIDs |
| Slow | 5 min | System Time (`.2.13.0`) |
| Very slow | 1 h (and at startup) | Product name, firmware version and date |

## Parameters

Raw SNMP values are polled into hidden parameters (PID 1110, 1200-1401) and converted by QAction 3.

| PID | Page | Parameter | OID (`1.3.6.1.4.1.18001.1.`) | Scale | Unit | Trend | Alarm |
|---|---|---|---|---|---|---|---|
| 100 | General | Product Name | 1.1.0 | – | – | No | No |
| 101 | General | Firmware Version | 1.2.0 | – | – | No | No |
| 102 | General | Firmware Date | 1.3.0 | – | – | No | No |
| 110 | General | System Time | 2.13.0 (DateAndTime) | decoded | – | No | No |
| 111 | General | Inverter Status | Alarm Code B0 | Inverter/Bypass | – | No | Yes |
| 112 | General | Main Supply Priority | 2.14.0 | 0x00AC AC / 0x00DC DC | – | No | Yes |
| 113 | General | Power State | 2.15.0 | 0xFF00 On / 0xFF0F Off | – | No | Yes |
| 114 | General | Timer Command/Status Raw | 2.16.0 | raw | – | No | No |
| 115 | General | Timer Remaining Raw | 2.17.0 | raw | – | No | No |
| 200 | Power | DC Input Voltage | 2.1.0 | ×0.1 | V | Yes | Yes |
| 201 | Power | AC Input Voltage | 2.2.0 | ×0.1 | V | Yes | Yes |
| 202 | Power | AC Input Frequency | 2.3.0 | ×0.1 | Hz | Yes | Yes |
| 300 | Power | Output Frequency | 2.4.0 | ×0.1 | Hz | Yes | Yes |
| 301 | Power | Output Voltage | 2.5.0 | ×0.1 | V | Yes | Yes |
| 302 | Power | Output Current | 2.6.0 | ×0.1 | A | Yes | Yes |
| 303 | Power | Output Power | 2.7.0 | ×1 | VA | Yes | Yes |
| 304 | Power | Load Ratio | 2.8.0 | ×0.1 | % | Yes | Yes |
| 400 | Environment | Internal Temperature | 2.9.0 | ×0.1 (signed 16-bit) | °C | Yes | Yes |
| 401 | Environment | Ambient Temperature | 2.10.0 | ×0.1 (signed 16-bit) | °C | Yes | Yes |
| 402 | Environment | Fan Status | Alarm Code B1 | Stop/Run | – | No | Yes |
| 403 | Environment | Sound Status | Alarm Code B2 | Off/On | – | No | No |
| 500 | Alarms | Alarm Code Raw | 2.11.0 | bitmask | – | No | No |
| 501 | Alarms | Active Alarm Summary | derived (B3-B15, excl. B9) | – | – | No | No |
| 502 | Alarms | Active Alarm Count | derived | – | – | Yes | No |
| 510-525 | Alarms | Alarm bits B0-B15 | derived from 500 | 0/1 | – | No | Yes (except 511, 512, 519) |

## Recommended alarm template

| PID | Condition | Severity |
|---|---|---|
| 111 / 510 | Bypass (if not expected) | Warning |
| 113 | Power Off | Critical |
| 402 | Stop (combine with high temperature) | Major |
| 513, 514, 517, 518, 520 | Bit = 1 | Critical |
| 515 | Overload 120% | Warning |
| 516, 521, 522, 524, 525 | Bit = 1 | Major |
| 523 | AC Input Missing | Major/Critical |
| 200-202, 300-301, 304, 400-401 | Low/High thresholds | To be confirmed by vendor |

Analog thresholds (voltage, frequency, load, temperature) are left configurable in the alarm template pending vendor confirmation.

## Validated values (simulation 10.6.100.242)

| Parameter | Raw | Displayed |
|---|---|---|
| DC Input Voltage | 544 | 54.4 V |
| AC Input Voltage | 1348 | 134.8 V |
| AC Input Frequency | 601 | 60.1 Hz |
| Output Current | 1 | 0.1 A |
| Output Power | 18 | 18 VA |
| Load Ratio | 9 | 0.9 % |
| Internal Temperature | 270 | 27.0 °C |
| Alarm Code | 1 | B0 = Bypass, no active alarms |
| System Time | 07 EA 0A 16 10 01 26 00 | 2026-10-22 16:01:38 |

## Open points (vendor confirmation)

- Meaning of OIDs `.2.16.0` and `.2.17.0` (exposed raw).
- `alarmStateChange` trap (`1.3.6.1.4.1.18001.1.3.1`) is defined in the MIB but not processed; alarm code is polled every 10 s.
- Analog thresholds and whether Bypass is a normal operating mode.
