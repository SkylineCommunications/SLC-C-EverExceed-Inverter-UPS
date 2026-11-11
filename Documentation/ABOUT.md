Looking for README.md? Check your Solution Root folder. This folder can be used to add documentation related to this solution.

# Documentation

## Index

| Folder / file | Content |
|---|---|
| `EverExceed Inverter UPS.md` | Connector documentation: polling, parameters, alarm template and open points. |
| `Simulations/` | SNMP simulation (QA Device Simulator XML) used to test the connector without a physical device. |

## Provenance

| Asset | Source | Type | Notes |
|---|---|---|---|
| `Simulations/EverExceed Inverter UPS.xml` | Recorded with the QA Device Simulator (auto-build 1.2) from the SNMP v2c agent of an EverExceed (An Power) inverter | **Real capture** | Covers every OID polled by the connector (`1.3.6.1.4.1.18001.1.*`), the MIB-2 system group (`sysObjectID = 1.3.6.1.4.1.18001.1`) and the `1.3.6.1.4.1.4976.3.3.1.*` OIDs exposed by the agent. Scrubbed before commit: SNMP agent framework, USM, VACM and community tables (`1.3.6.1.6.3.*`, containing the device community string and engine ID) were removed, and the agent IP was replaced by `127.0.0.1`. Set the agent IP to the IP of the simulator host before loading. |

## Missing material

- `AN-POWER-INVERTER-MIB` vendor MIB file (to be stored under `MIBs/`).
- Vendor confirmation of the meaning of OIDs `.2.16.0` and `.2.17.0`.
