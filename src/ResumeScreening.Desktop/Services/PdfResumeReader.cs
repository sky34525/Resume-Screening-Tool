using System.Text;
using PDFtoImage;
using PDFtoImage.Exceptions;
using ResumeScreening_Desktop.Models;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace ResumeScreening_Desktop.Services;

/// <summary>
/// 简历数据
/// </summary>
/// <param name="FirstPagePng">PDF 首页预览图的 PNG 二进制数据</param>
/// <param name="Pages">PDF 页面和对应的文字</param>
internal sealed record PdfResumeReadResult(
    byte[] FirstPagePng,
    IReadOnlyList<ResumePage> Pages);

internal static class PdfResumeReader
{
    private const int PreviewWidth = 560;
    private const int MaxPdfPages = 100;

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

        var pages = ExtractPagesBestEffort(pdfBytes, pageCount);
        return new PdfResumeReadResult(previewPng, pages);
    }

    private static IReadOnlyList<ResumePage> ExtractPagesBestEffort(byte[] pdfBytes, int expectedPageCount)
    {
        var pages = new List<ResumePage>(expectedPageCount);

        try
        {
            using var document = PdfDocument.Open(pdfBytes);

            for (var pageNumber = 1; pageNumber <= expectedPageCount; pageNumber++)
            {
                var text = string.Empty;

                try
                {
                    var page = document.GetPage(pageNumber);
                    text = ContentOrderTextExtractor.GetText(page).Trim();
                    pages.Add(new ResumePage(pageNumber, text));
                }
                catch
                {
                    pages.Add(new ResumePage(pageNumber, string.Empty));
                }

            }

        }
        catch
        {
            pages.Clear();

            // PdfPig 完全无法打开时，仍保留 PDFium 检测到的物理页数。
            for (var pageNumber = 1; pageNumber <= expectedPageCount; pageNumber++)
            {
                pages.Add(new ResumePage(pageNumber, string.Empty));
            }
        }

        return pages;
    }
}
