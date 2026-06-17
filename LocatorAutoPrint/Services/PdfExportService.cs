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
            Document doc = new Document(PageSize.A4);
            try
            {
                PdfWriter.GetInstance(doc, new FileStream(filePath, FileMode.Create));
                doc.Open();

                // Header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                var rowFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

                doc.Add(new Paragraph("INF (Item Not Found) Report", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20 });
                doc.Add(new Paragraph($"Generated: {DateTime.Now:MM/dd/yyyy hh:mm:ss tt}") { SpacingAfter = 10 });

                // Table
                PdfPTable table = new PdfPTable(6);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 10f, 10f, 15f, 15f, 40f, 10f });

                string[] headers = { "SlotNo", "RecNo", "UPC", "SKU", "Description", "Qty" };
                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 };
                    table.AddCell(cell);
                }

                foreach (var rec in records)
                {
                    table.AddCell(new PdfPCell(new Phrase(rec.SlotNo, rowFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(rec.RecNo.ToString(), rowFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(rec.UPC, rowFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(rec.SKU, rowFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(rec.Descr, rowFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(rec.Qty.ToString("0.##"), rowFont)) { Padding = 5 });
                }

                doc.Add(table);
            }
            finally
            {
                doc.Close();
            }
        }
    }
}