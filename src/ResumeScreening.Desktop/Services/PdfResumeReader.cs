using System.Text;
using PDFtoImage;
using PDFtoImage.Exceptions;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace ResumeScreening_Desktop.Services;

internal sealed record PdfResumeReadResult(
    int PageCount,
    byte[] FirstPagePng,
    string ExtractedText);

internal static class PdfResumeReader
{
    private const int PreviewWidth = 560;
    private const int MaxPdfPages = 100;
    private const int MaxExtractedTextLength = 200_000;

    public static Task<PdfResumeReadResult> ReadAsync(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        return Task.Run(() => Read(pdfBytes));
    }

    private static PdfResumeReadResult Read(byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0)
        {
            throw new InvalidDataException("PDF 文件为空");
        }

        int pageCount;
        byte[] previewPng;

        try
        {
            pageCount = Conversion.GetPageCount(pdfBytes);
            if (pageCount == 0)
            {
                throw new InvalidDataException("PDF 中没有页面");
            }

            if (pageCount > MaxPdfPages)
            {
                throw new InvalidDataException($"PDF 超过 {MaxPdfPages} 页上限");
            }

            var renderOptions = new RenderOptions
            {
                Width = PreviewWidth,
                WithAspectRatio = true,
                WithAnnotations = true,
                WithFormFill = true
            };

            using var firstPage = Conversion.ToImage(
                pdfBytes,
                page: 0,
                password: null,
                options: renderOptions);
            using var encodedPng = firstPage.Encode(SKEncodedImageFormat.Png, 100);

            if (encodedPng is null || encodedPng.Size == 0)
            {
                throw new InvalidDataException("PDF 首页无法生成预览");
            }

            previewPng = encodedPng.ToArray();
        }
        catch (PdfPasswordProtectedException exception)
        {
            throw new InvalidDataException("PDF 受密码保护，需要先解锁后再导入", exception);
        }
        catch (PdfUnsupportedSecuritySchemeException exception)
        {
            throw new InvalidDataException("PDF 使用了不受支持的安全方案", exception);
        }
        catch (PdfException exception)
        {
            throw new InvalidDataException("PDF 无法解析或文件已损坏", exception);
        }

        var extractedText = ExtractTextBestEffort(pdfBytes);
        return new PdfResumeReadResult(pageCount, previewPng, extractedText);
    }

    private static string ExtractTextBestEffort(byte[] pdfBytes)
    {
        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            var builder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var pageText = ContentOrderTextExtractor.GetText(page).Trim();
                if (pageText.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine().AppendLine();
                }

                var remainingLength = MaxExtractedTextLength - builder.Length;
                if (remainingLength <= 0)
                {
                    break;
                }

                builder.Append(pageText.AsSpan(0, Math.Min(pageText.Length, remainingLength)));
            }

            return builder.ToString().Trim();
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new InvalidDataException("PDF 受密码保护，需要先解锁后再导入", exception);
        }
        catch
        {
            // PDFium can still render PDFs whose text structure PdfPig cannot parse.
            // Keep the preview and identify the document as image-only/no-text.
            return string.Empty;
        }
    }
}
