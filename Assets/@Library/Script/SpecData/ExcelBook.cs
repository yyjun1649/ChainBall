// Assets/Editor/SpecData/ExcelBook.cs
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;

namespace SpecData.EditorTools
{
    /// <summary>
    /// 엑셀 북을 문자열 2차원 배열 형태의 시트로 읽어들이는 래퍼.
    /// 모든 셀은 문자열로 통일(형변환은 TypeMapper/RowParser 단계에서).
    /// 행/열은 1-based (엑셀 기준).
    /// </summary>
    public sealed class ExcelBook
    {
        public readonly Dictionary<string, ExcelSheet> Sheets = new();

        public static ExcelBook Open(string path)
        {
            var book = new ExcelBook();
            // FileShare.ReadWrite: Excel이 파일을 연 상태(독점 잠금)에서도 읽을 수 있도록 공유 모드로 연다.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var wb = new XLWorkbook(fs);
            foreach (var ws in wb.Worksheets)
                book.Sheets[ws.Name] = ExcelSheet.From(ws);
            return book;
        }
    }

    public sealed class ExcelSheet
    {
        public string Name;
        public int RowCount;   // 1-based
        public int ColCount;   // 1-based
        public string[,] Cells;

        public string Cell(int row, int col)
        {
            if (row < 1 || row > RowCount || col < 1 || col > ColCount) return string.Empty;
            return Cells[row - 1, col - 1] ?? string.Empty;
        }

        public static ExcelSheet From(IXLWorksheet ws)
        {
            var s = new ExcelSheet { Name = ws.Name };
            var lastRow = ws.LastRowUsed();
            var lastCol = ws.LastColumnUsed();
            if (lastRow == null || lastCol == null) { s.Cells = new string[0, 0]; return s; }

            // 절대 행/열 번호로 잡아야 1행이 비어있는 시트도 올바르게 인덱싱된다.
            int rows = lastRow.RowNumber();
            int cols = lastCol.ColumnNumber();
            s.RowCount = rows;
            s.ColCount = cols;
            s.Cells = new string[rows, cols];

            for (int r = 1; r <= rows; r++)
            for (int c = 1; c <= cols; c++)
            {
                var cell = ws.Cell(r, c);
                // 엑셀이 1000 → "1,000" 처럼 포매팅해도 원본 숫자를 얻도록
                string str;
                if (cell.DataType == XLDataType.Number)
                    str = cell.GetDouble().ToString("R", CultureInfo.InvariantCulture);
                else if (cell.DataType == XLDataType.Boolean)
                    str = cell.GetBoolean() ? "true" : "false";
                else
                    str = cell.GetString();
                s.Cells[r - 1, c - 1] = str ?? string.Empty;
            }
            return s;
        }
    }
}
