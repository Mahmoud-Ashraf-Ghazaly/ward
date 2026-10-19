using System;
using System.Text;
using ZXing;
using ZXing.Common;

namespace ward.Services.Barcode
{
    public interface IBarcodeService
    {
        string GenerateUniqueBarcode(string prefix = "FLW");
        string GenerateCode128Svg(string content, int height = 80, int barWidth = 2);
        string? DecodeBarcodeFromPixelData(byte[] rgbBytes, int width, int height);
        bool IsValidBarcode(string barcode);
    }

    public class BarcodeService : IBarcodeService
    {
        private static readonly Random _random = new Random();

        // Standard Code 128B patterns (107 patterns, each 11 modules wide, stop pattern is 13 modules)
        private static readonly string[] Code128Patterns = new string[]
        {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
            "114131", "311141", "411131", "211412", "211214", "211232", "2331112" // 100-106 (104=Start B, 106=Stop)
        };

        public string GenerateUniqueBarcode(string prefix = "FLW")
        {
            // Generate a clean Code 128 / EAN compatible alphanumeric identifier
            var timestamp = DateTime.UtcNow.ToString("yyMMddHHmm");
            var rand = _random.Next(100, 999);
            return $"{prefix}-{timestamp}-{rand}";
        }

        public bool IsValidBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode) || barcode.Length < 3 || barcode.Length > 50)
                return false;

            // Allow standard alphanumeric and hyphens/underscores
            foreach (var ch in barcode)
            {
                if (ch < 32 || ch > 126) return false;
            }
            return true;
        }

        public string GenerateCode128Svg(string content, int height = 80, int barWidth = 2)
        {
            if (string.IsNullOrEmpty(content))
                content = "000000";

            // Use Code 128 Subset B (ASCII 32 to 127)
            var codes = new System.Collections.Generic.List<int>();
            const int startB = 104;
            const int stopPatternIndex = 106;

            codes.Add(startB);
            int checksum = startB;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                int codeVal = c - 32;
                if (codeVal < 0 || codeVal > 95) codeVal = 0; // fallback to space
                codes.Add(codeVal);
                checksum += codeVal * (i + 1);
            }

            int checkDigit = checksum % 103;
            codes.Add(checkDigit);
            codes.Add(stopPatternIndex);

            // Calculate widths
            var totalModules = 0;
            var patternList = new System.Collections.Generic.List<string>();

            foreach (var code in codes)
            {
                var pattern = Code128Patterns[code];
                patternList.Add(pattern);
                foreach (char ch in pattern)
                {
                    totalModules += (ch - '0');
                }
            }

            int quietZoneModules = 10;
            int totalWidthModules = totalModules + (quietZoneModules * 2);
            int totalSvgWidth = totalWidthModules * barWidth;
            int totalSvgHeight = height + 24; // Extra space for human-readable text

            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{totalSvgWidth}\" height=\"{totalSvgHeight}\" viewBox=\"0 0 {totalSvgWidth} {totalSvgHeight}\">");
            sb.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

            int currentX = quietZoneModules * barWidth;

            foreach (var pattern in patternList)
            {
                bool isBar = true;
                foreach (char ch in pattern)
                {
                    int width = (ch - '0') * barWidth;
                    if (isBar)
                    {
                        sb.Append($"<rect x=\"{currentX}\" y=\"10\" width=\"{width}\" height=\"{height}\" fill=\"#000000\"/>");
                    }
                    currentX += width;
                    isBar = !isBar;
                }
            }

            // Add human readable text
            sb.Append($"<text x=\"{totalSvgWidth / 2}\" y=\"{height + 20}\" text-anchor=\"middle\" font-family=\"monospace, sans-serif\" font-size=\"14\" font-weight=\"bold\" fill=\"#333333\">{content}</text>");
            sb.Append("</svg>");

            return sb.ToString();
        }

        public string? DecodeBarcodeFromPixelData(byte[] rgbBytes, int width, int height)
        {
            try
            {
                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options = new DecodingOptions
                    {
                        TryHarder = true,
                        PossibleFormats = new[] { BarcodeFormat.CODE_128, BarcodeFormat.EAN_13, BarcodeFormat.QR_CODE }
                    }
                };

                var luminanceSource = new RGBLuminanceSource(rgbBytes, width, height);
                var result = reader.Decode(luminanceSource);
                return result?.Text;
            }
            catch
            {
                return null;
            }
        }
    }
}
