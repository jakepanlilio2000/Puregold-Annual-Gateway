# Puregold Annual Gateway (v3.5) — Quick-Start Operational Guide

> **Store Associate & Inventory Supervisor Quick Reference**  
> *Everything you need to know for day-to-day physical count processing.*

---

## ⚡ What's New in Version 3.5

1. **Official Puregold Green & Gold Theme:** Clear visual hierarchy with Emerald Green action headers and high-contrast tables.
2. **5-Tab Navigation:** Direct access to `Print`, `Edit Sheet`, `Reports`, `Users`, and `About`.
3. **One-Click Software Updater:** Check for system updates from the store intranet server directly inside the `About` tab.
4. **Unlocked Scrolling Tables:** View and scroll through all summary records, stock values, and system users without clipping or truncation.
5. **Instant Mobile Session Reset:** Easily clear stuck scanner sessions with the **Logout Mobile** button.

---

## 📋 3-Step Daily Inventory Workflow

### Step 1: Verify System Readiness
* Check the bottom-left footer: Ensure **SQL Server Connected** is displayed in green.
* Check the top-right header: Verify **Sessions** count reflects active handheld scanners.

### Step 2: Print Locator Countsheets
1. Click the **Print** tab.
2. In the input box, type the locator numbers you need to print (only digits separated by commas, such as `1,2,3` or `34,87,100`; no other formats are supported).
3. Click **Print** or press **Enter**.
4. The system automatically creates a local backup file and prints receipts to the default printer.
   * *Note:* If a locator is still **OPEN**, ask the counting team to close and transmit it first.

### Step 3: Correct or Add Count Items
1. Click the **Edit Sheet** tab.
2. Enter the **Locator** and **Record No.** to inspect an item.
3. To adjust quantities:
   * Click **Edit**.
   * Enter the verified physical quantity in the **Quantity** field.
   * Click **Save**.
4. To add an unlisted item found on the sales floor:
   * Click **Add**.
   * Enter or scan the UPC/SKU, input description and quantity, then click **Save**.
5. Click **Print Edited** to generate an updated verification count slip.

---

## 🔍 Masterfile Search & Discrepancy Auditing

* **Searching Masterfile Items:**
  * Open the **Reports** tab &rarr; Click **SKU Inquiry**.
  * Type any keyword, brand, or SKU number and press **Enter**.
  * Right-click any row to copy the barcode/UPC or send the item to the countsheet masterfile.
* **Reviewing Missing Items (INF):**
  * Open the **Reports** tab &rarr; Click **INF Report** &rarr; Click **Generate INF Report**.
  * Click **Print INF Report** to generate a discrepancy list for the inventory head.

---

## 🚀 Common Questions & Quick Fixes

| Question / Problem | Solution |
| :--- | :--- |
| **"User is stuck or cannot login to scanner"** | Go to the **Users** tab &rarr; Click the user's name &rarr; Click **Logout Mobile**. The user can log in immediately. |
| **"Printer stopped printing halfway through"** | Check paper roll in the receipt printer. The system recorded all count data safely; simply re-enter the remaining locators and press **OK**. |
| **"How do I update the application?"** | Go to the **About** tab &rarr; Click **Check for Updates**. If an update is found, click **Download Update**. |
| **"Accidental wrong quantity saved"** | Open **Edit Sheet**, re-load the locator and record number, type the correct quantity, and click **Save**. |

---

## 📞 Support & Helpdesk
* **Local Store IT:** Call **Local 722**
* **Technical Lead:** Jash (Jake Panlilio) — IT SF1 (722), Zone 11

