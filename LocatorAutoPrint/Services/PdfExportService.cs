using System;
using System.Collections.Generic;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class PdfExportService
    {
        public void ExportInfReportToPdf(List<InfReportModel> records, string filePath)
        {
            if (records == null) records = new List<InfReportModel>();

            Document doc = new Document(PageSize.A4, 36f, 36f, 36f, 36f);
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    // Header
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                    var rowFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

                    doc.Add(new Paragraph("INF (Item Not Found) Report", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 15 });
                    doc.Add(new Paragraph($"Generated: {DateTime.Now:MM/dd/yyyy hh:mm:ss tt} | Total Records: {records.Count}") { SpacingAfter = 10 });

                    // Table
                    PdfPTable table = new PdfPTable(6);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 12f, 10f, 18f, 15f, 35f, 10f });

                    string[] headers = { "SlotNo", "RecNo", "UPC", "SKU", "Description", "Qty" };
                    foreach (var header in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                        {
                            BackgroundColor = BaseColor.LIGHT_GRAY,
                            Padding = 6,
                            HorizontalAlignment = Element.ALIGN_CENTER
                        };
                        table.AddCell(cell);
                    }

                    foreach (var rec in records)
                    {
                        table.AddCell(new PdfPCell(new Phrase(rec.SlotNo ?? string.Empty, rowFont)) { Padding = 5 });
                        table.AddCell(new PdfPCell(new Phrase(rec.RecNo.ToString(), rowFont)) { Padding = 5, HorizontalAlignment = Element.ALIGN_RIGHT });
                        table.AddCell(new PdfPCell(new Phrase(rec.UPC ?? string.Empty, rowFont)) { Padding = 5 });
                        table.AddCell(new PdfPCell(new Phrase(rec.SKU ?? string.Empty, rowFont)) { Padding = 5 });
                        table.AddCell(new PdfPCell(new Phrase(rec.Descr ?? string.Empty, rowFont)) { Padding = 5 });
                        table.AddCell(new PdfPCell(new Phrase(rec.Qty.ToString("0.##"), rowFont)) { Padding = 5, HorizontalAlignment = Element.ALIGN_RIGHT });
                    }

                    doc.Add(table);
                }
            }
            finally
            {
                if (doc.IsOpen())
                {
                    try { doc.Close(); } catch { }
                }
            }
        }
    }
}