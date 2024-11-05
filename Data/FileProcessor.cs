﻿using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace SemanticKernelFun.Data;

public class RawContentDocument
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string FileName { get; set; }
    public string Text { get; set; }
}

public class FileProcessor
{
    public static async Task<List<RawContentDocument>> ProcessFiles(string folderPath)
    {
        var rawContentDocuments = new List<RawContentDocument>();
        var searchPatterns = new[] { "*.txt", "*.pdf", "*.csv", "*.doc", "*.docx", "*.xlsx" };

        var files = searchPatterns.SelectMany(pattern => Directory.GetFiles(folderPath, pattern));

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file).ToLower();
            var rawContentDocument = extension switch
            {
                ".txt" => await ProcessTxtAsync(file),
                ".pdf" => ProcessPdf(file),
                ".csv" => ProcessCsv(file),
                ".doc" or ".docx" => ProcessWord(file),
                ".xlsx" => ProcessExcel(file),
                _ => throw new NotImplementedException()
            };

            rawContentDocument.FileName = Path.GetFileName(file);

            rawContentDocuments.Add(rawContentDocument);
        }

        return rawContentDocuments;
    }

    private static async Task<RawContentDocument> ProcessTxtAsync(string filePath)
    {
        var text = await File.ReadAllTextAsync(filePath);

        return new RawContentDocument() { Text = text };
    }

    private static RawContentDocument ProcessPdf(string filePath)
    {
        var text = new StringBuilder();

        using var pdfDocument = PdfDocument.Open(filePath);
        foreach (var page in pdfDocument.GetPages())
        {
            text.AppendLine(page.Text);
        }

        return new RawContentDocument() { Text = text.ToString() };
    }

    private static RawContentDocument ProcessCsv(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
        var records = csv.GetRecords<dynamic>();
        var text = string.Join(Environment.NewLine, records.Select(r => string.Join(", ", r)));

        return new RawContentDocument() { Text = text };
    }

    private static RawContentDocument ProcessWord(string filePath)
    {
        var text = new StringBuilder();

        using (var wordDoc = WordprocessingDocument.Open(filePath, false))
        {
            foreach (var element in wordDoc.MainDocumentPart.Document.Body.Elements<Paragraph>())
            {
                text.AppendLine(element.InnerText);
            }
        }

        return new RawContentDocument { Text = text.ToString() };
    }

    private static RawContentDocument ProcessExcel(string filePath)
    {
        var text = new StringBuilder();

        using var workbook = new XLWorkbook(filePath);
        foreach (var worksheet in workbook.Worksheets)
        {
            var worksheetContent = new StringBuilder();

            foreach (var row in worksheet.RowsUsed())
            {
                var rowValues = row.Cells().Select(cell => cell.GetValue<string>());
                worksheetContent.AppendLine(string.Join(", ", rowValues));
            }

            text.AppendLine(worksheetContent.ToString().Trim());
        }

        return new RawContentDocument() { Text = text.ToString() };
    }
}