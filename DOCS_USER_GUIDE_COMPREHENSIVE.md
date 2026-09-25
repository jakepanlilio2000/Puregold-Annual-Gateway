# Puregold Annual Gateway — Comprehensive Technical & Operational Manual
**Version:** 3.5 (Production Release)  
**System Classification:** Store Inventory Reconciliation & Automated Locator Processing Gateway  
**Author / Engineering:** Jash (Jake Panlilio) — IT SF1 (722), Zone 11 © 2026  
**Target Environment:** Puregold Store Intranet & POS Back-Office Infrastructure  

---

## 1. System Overview & Architecture

Puregold Annual Gateway (`LocatorAutoPrint.exe`) is an enterprise-grade Windows desktop gateway engineered for retail store inventory auditing, real-time countsheet adjustments, and high-throughput locator printing. 

```
                                +-----------------------------------+
                                |    Puregold Annual Gateway v3.5   |
                                |     (WPF .NET Framework 4.8)      |
                                +-----------------+-----------------+
                                                  |
                 +--------------------------------+-------------------------------+
                 |                                |                               |
                 v                                v                               v
     +-----------------------+        +-----------------------+       +-----------------------+
     |   MS SQL Server DB    |        |  Intranet FTP Server  |       | Store Network Printer |
     | - PUREGOLD (Main)     |        | - 192.168.200.177     |       | - Windows Default     |
     | - exclusivesdb        |        | - A&VG Executables    |       |   Thermal or Laser    |
     | - AGING_DB (Users)    |        | - Crash Telemetry Logs|       |   Document Spooler    |
     +-----------------------+        +-----------------------+       +-----------------------+
```

### 1.1 Technical Stack & Prerequisites
* **Runtime:** Microsoft .NET Framework 4.8.
* **Architecture:** x64 / x86 compatible, packed with Costura.Fody for single-binary portability.
* **Databases:** Microsoft SQL Server 2008 R2 / 2012 / 2016 / 2019 running databases:
  * `PUREGOLD`: Primary countsheet (`COUNTSHEET`), inventory masterfile, and item descriptions.
  * `exclusivesdb`: Secondary item classifications and locator statuses.
  * `AGING_DB`: User credentials, mobile session tokens, and IP device registry.
* **Update & Telemetry Host:** Local Store Intranet FTP (`ftp://192.168.200.177`).
* **Display Requirement:** Fixed 565x565 viewport resolution optimized for POS back-office monitors.

---

## 2. System Configuration & Data Contracts

### 2.1 Application Configuration (`config.json`)
Located in the application root directory:
```json
{
  "Server": "192.168.200.100",
  "Port": 1433,
  "Database": "PUREGOLD",
  "UserId": "sa",
  "Password": "encrypted_or_clear_password",
  "StoreName": "PUREGOLD SAN FERNANDO",
  "AppPort": 982,
  "PrintBackupDirectory": "C:\\LocatorAutoPrint\\Backups"
}
```

### 2.2 Preserved 89-Character Text Backup Contract
During print runs, countsheets are backed up to local flat text files before spooling to the printer. This ensures 100% downstream disaster recovery compatibility:
* Columns: `SlotNo[4] + RecNo[4] + CountDate[18] + UPC[15] + SKU[8] + Descr[32] + Qty[8] = 89 characters`.
* Restoring locators using the Database Restore utility re-populates both `OriginalQty` and `EditedQty` idempotently.

---

## 3. Operational Modules & Workflows

### 3.1 Locator Printing (`Print` Tab)
1. **Input Format:** Enter locator numbers as digits separated by commas (such as `1,2,3` or `34,87,100`). No other formats are supported.
2. **Pre-flight Validation:**
   * The gateway checks whether each locator is flagged as **Closed** (`Status.Closed == true`). If open, printing is halted for that locator with a diagnostic warning.
   * If count records exist, an 89-character backup file is generated in the `Backup` folder.
3. **Spooling:** Count records are rendered into high-density receipts and dispatched to the default Windows printer.
4. **Re-entrancy Protection:** The print action disables inputs and shows a progress overlay to prevent duplicate print jobs.

### 3.2 Edit Count Sheet (`Edit Sheet` Tab)
1. **Lookup:** Enter **Locator** and **Record No.** to populate item data.
2. **Masterfile Lookup:**
   * Entering a UPC or SKU triggers an automatic masterfile query on Enter or field defocus.
   * If the item is absent from the local countsheet, use **Add** to register new items.
3. **Data Integrity Guarantee:**
   * The **Original Count** is immutable and preserved in audit records.
   * The **Quantity** field updates `EditedQty`. Negative numbers and blank inputs are blocked at the view layer.
4. **Print Edited:** Prints updated countsheet records directly for immediate auditing.

### 3.3 Reports & Inventory Inquiries (`Reports` Tab)
* **INF Report:** Identifies "Item Not Found" barcodes scanned during physical inventory. Supports direct hardware printing.
* **SKU Inquiry:** Live search across product masterfiles by keyword, SKU, or UPC. Right-click context menus allow instant clipboard copying (`UPC`, `SKU`, `Description`, or all columns) and "Add to Masterfile".
* **Summary Report:** Aggregates record counts, total piece quantities, SKU diversity, and remarks per locator slot.
* **Stock Value:** Displays SKU-level on-hand balances, average unit costs (`₱`), and extended inventory values.

### 3.4 User & Mobile Device Management (`Users` Tab)
* **Live Connectivity:** Pings scanner device IP addresses; green dots indicate active online handheld units.
* **Mobile Session Clear:** If a handheld scanner loses Wi-Fi connection and leaves an active locator locked, select the user and click **Logout Mobile** to release the session token immediately without restarting SQL Server.

### 3.5 About & Intranet Auto-Updater (`About` Tab)
* **System Metadata:** Displays build version (`v3.5`), host machine name, process architecture, and store IT support contact numbers.
* **Update Verification:**
  1. Click **Check for Updates** to connect to `ftp://192.168.200.177`.
  2. The system compares remote `A&VG*.exe` versions against `3.5`.
  3. If a higher version exists, click **Download Update** to retrieve the installer directly into `Desktop\Puregold Updates\`.

---

## 4. Troubleshooting & Error Recovery

| Symptom / Error | Root Cause | Resolution Protocol |
| :--- | :--- | :--- |
| **"SQL Server Disconnected"** in footer | Network interface down, incorrect IP in `config.json`, or SQL Server service stopped. | 1. Verify ethernet cable.<br>2. Ping server IP in command prompt.<br>3. Restart `MSSQLSERVER` on host server. |
| **"Locator X: Currently OPEN"** | Floor auditors have not closed the locator in the scanner application. | Instruct scanner team to transmit and finalize the locator before printing. |
| **"Unable to reach update server"** | Gateway machine cannot access store intranet FTP server (192.168.200.177). | Verify intranet routing. Updates can still be manually transferred via authorized IT USB flash drive. |
| **Duplicate printouts occurred** | Spooler queue backlog. | Double-click protection in v3.5 prevents software double-triggers. Purge Windows printer queue if hardware buffer was stalled. |
| **Zeroed quantities after restore** | Legacy v3.3 bug resolved in v3.4/v3.5. | Run v3.5 restore tool. Fixed query preserves `@qty` for both original and edited quantity fields. |
| **Scanner user locked to locator** | Handheld battery died during transmission. | Open **Users** tab &rarr; Select username &rarr; Click **Logout Mobile**. |

---

## 5. Technical Support Pathways

* **Store Level:** Store IT Department (Local Extension: 722)
* **Lead Engineer:** Jash (Jake Panlilio) — IT SF1 (722), Zone 11
* **Escalation Email:** `jpanlilio@puregold.com.ph`
* **Intranet Repository:** `ftp://192.168.200.177/toho/(722)San_Fernando/Others/Annual Gateway/`

