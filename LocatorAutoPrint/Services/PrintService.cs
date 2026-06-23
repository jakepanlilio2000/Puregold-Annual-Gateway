using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class PrintService
    {
        private readonly string _appBaseDir;

        public PrintService(string appBaseDir)
        {
            _appBaseDir = appBaseDir;
        }

        public void BackupToTextFile(int locatorNo, List<CountRecord> records)
        {
            string backupFolder = Path.Combine(_appBaseDir, "cntsheet");
            if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);

            var sb = new StringBuilder();
            foreach (var rec in records)
            {
                string colLoc = locatorNo.ToString().PadRight(4);
                string colRec = rec.RecNo.PadRight(4);
                string colDate = rec.FormattedDate.PadRight(18);
                string colUpc = rec.UPC.PadRight(15);
                string colSku = rec.SKU.PadRight(8);
                string colDesc = rec.Descr.PadRight(32);
                string colQty = rec.RawQtyForBackup.PadRight(8);

                sb.AppendLine($"{colLoc}{colRec}{colDate}{colUpc}{colSku}{colDesc}{colQty}");
            }

            File.WriteAllText(Path.Combine(backupFolder, $"{locatorNo}.txt"), sb.ToString(), Encoding.ASCII);
        }

        public async Task PrintLocatorSheetAsync(int locatorNo, string storeName, List<CountRecord> records)
        {
            var sb = new StringBuilder();
            var now = DateTime.Now;

            double grandTotalQty = records.Sum(r => r.CleanQty);
            int infCount = records.Count(r => r.Descr.Trim() == "INF");
            long sumSku = records.Sum(r => long.TryParse(r.SKU, out long sku) ? sku : 0);

            sb.AppendLine($"{now:hh:mm tt} {now:MM/dd/yyyy}");
            sb.AppendLine(CenterText(storeName));
            sb.AppendLine(CenterText("Annual Inventory Count"));
            sb.AppendLine("");
            sb.AppendLine(CenterText("***** Initial Count Sheet *****"));
            sb.AppendLine("");
            sb.AppendLine($"Locator No. : {locatorNo}");
            sb.AppendLine($"Count Date. : {now:MM/dd/yyyy}");
            sb.AppendLine("");
            sb.AppendLine("Rec No    UPC         SKU   Description                               Count    Remarks");
            sb.AppendLine("");

            foreach (var rec in records)
            {
                string col1 = rec.RecNo.PadRight(5);
                string col2 = rec.UPC.PadRight(15);
                string col3 = rec.SKU.PadRight(6);
                string col4 = rec.Descr.PadRight(40);
                string col5 = rec.Qty.PadRight(5);
                string col6 = "_______";

                sb.AppendLine($"{col1} {col2} {col3} {col4} {col5}    {col6}");
            }

            sb.AppendLine("");
            string grandTotalStr = grandTotalQty % 1 == 0 ? grandTotalQty.ToString("0") : grandTotalQty.ToString("0.###");
            sb.AppendLine($"Number of Records Scanned: {records.Count}".PadRight(52) + $"GRAND TOTAL : {grandTotalStr}");
            sb.AppendLine($"No. of INF Found : {infCount}");
            sb.AppendLine($"Sum of SKU Scanned : {sumSku}");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("       ____________            ____________                ____________");
            sb.AppendLine("        Scanned  By              Counted By                  Checked By");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("             ____________                    ____________");
            sb.AppendLine("               Team Leader                     Posted By");
            sb.AppendLine("");
            sb.AppendLine("");

            string tempPath = Path.Combine(Path.GetTempPath(), $"Locator_{locatorNo}.txt");
            File.WriteAllText(tempPath, sb.ToString(), Encoding.ASCII);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Verb = "Print",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                await Task.Delay(3000);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    await Task.Delay(1000);
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        private string CenterText(string text, int width = 80)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length >= width) return text;
            int spaces = (width - text.Length) / 2;
            return new string(' ', spaces) + text;
        }

        public async Task PrintPdfFileAsync(string filePath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "Print",
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };

                System.Diagnostics.Process.Start(psi);

                
                await Task.Delay(5000);
            }
            finally
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(15000);
                    try
                    {
                        if (System.IO.File.Exists(filePath))
                            System.IO.File.Delete(filePath);
                    }
                    catch { /* Ignore if file is still locked by the PDF viewer */ }
                });
            }
        }

        public async Task PrintInfReportAsync(List<InfReportModel> records)
        {
            if (records == null || records.Count == 0) return;

            var sb = new StringBuilder();
            var now = DateTime.Now;

            sb.AppendLine($"{now:hh:mm tt} {now:MM/dd/yyyy}");
            sb.AppendLine(CenterText("***** ITEM NOT FOUND (INF) REPORT *****"));
            sb.AppendLine("");
            sb.AppendLine($"Total INF Records: {records.Count}");
            sb.AppendLine("");
            sb.AppendLine("Locator  Rec No  UPC             SKU     Description                              Qty");
            sb.AppendLine("-------------------------------------------------------------------------------------");

            foreach (var rec in records)
            {
                string colLoc = rec.SlotNo.PadRight(9);
                string colRec = rec.RecNo.ToString().PadRight(8);
                string colUpc = rec.UPC.PadRight(16);
                string colSku = rec.SKU.PadRight(8);

                string descr = rec.Descr.Length > 38 ? rec.Descr.Substring(0, 38) : rec.Descr;
                string colDesc = descr.PadRight(41);

                string colQty = rec.Qty.ToString();

                sb.AppendLine($"{colLoc}{colRec}{colUpc}{colSku}{colDesc}{colQty}");
            }

            sb.AppendLine("-------------------------------------------------------------------------------------");
            sb.AppendLine(CenterText("***** END OF REPORT *****"));

            string tempPath = Path.Combine(Path.GetTempPath(), "INF_Report.txt");
            File.WriteAllText(tempPath, sb.ToString(), Encoding.ASCII);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Verb = "Print",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                await Task.Delay(3000);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    await Task.Delay(1000);
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
        public async Task PrintEditedLocatorSheetAsync(int locatorNo, string storeName, LocatorPrintSummary summary)
        {
            var sb = new StringBuilder();
            var now = DateTime.Now;

            sb.AppendLine($"{now:hh:mm tt} {now:MM/dd/yyyy}");
            sb.AppendLine(CenterText(storeName));
            sb.AppendLine(CenterText("Annual Inventory Count"));
            sb.AppendLine("");
            sb.AppendLine(CenterText("*****    Edited Count Sheet   *****"));
            sb.AppendLine("");
            sb.AppendLine($"Locator No. : {locatorNo}");
            sb.AppendLine($"Count Date. : {summary.CountDate}");
            sb.AppendLine("");
            sb.AppendLine("Rec No    UPC            SKU   Description                              Old   Edited");
            sb.AppendLine("                                                                        Qty    Qty");
            sb.AppendLine("");

            foreach (var rec in summary.EditedRecords)
            {
                string col1 = rec.RecNo.PadRight(10);
                string col2 = rec.UPC.PadRight(15);
                string col3 = rec.SKU.PadRight(6);
                string col4 = rec.Descr.PadRight(41);
                string col5 = rec.OldQtyStr.PadRight(6);
                string col6 = rec.EditedQtyStr;

                sb.AppendLine($"{col1}{col2}{col3}{col4}{col5}{col6}");
            }

            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine($"Number of Records Edited : {summary.TotalEdited}");
            sb.AppendLine($"Number of Records Added : {summary.TotalAdded}");
            sb.AppendLine("");
            sb.AppendLine("");

            string grandTotalStr = summary.GrandTotal % 1 == 0 ? summary.GrandTotal.ToString("0") : summary.GrandTotal.ToString("0.###");
            sb.AppendLine($"Number of Records Scanned: {summary.TotalScanned}".PadRight(52) + $"GRAND TOTAL : {grandTotalStr}");
            sb.AppendLine($"No. of INF Found : {summary.InfCount}");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("       ____________            ____________               ____________");
            sb.AppendLine("        Scanned  By             Counted By                 Checked By");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("              ____________                    ____________");
            sb.AppendLine("               Team Leader                     Posted By");
            sb.AppendLine("");

            string tempPath = Path.Combine(Path.GetTempPath(), $"Edited_Locator_{locatorNo}.txt");
            File.WriteAllText(tempPath, sb.ToString(), Encoding.ASCII);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Verb = "Print",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                System.Diagnostics.Process.Start(psi);
                await Task.Delay(3000);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    await Task.Delay(1000);
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
    }
}