// Assets/Editor/SpecData/TypeMapper.cs
namespace SpecData.EditorTools
{
    public enum Primitive { Int, Long, Float, Double, Bool, String, Enum }

    public sealed class FieldType
    {
        public Primitive Prim;
        public bool IsArray;
        public string EnumName;  // Prim == Enum 일 때만 사용

        public string ToCSharp()
        {
            string baseStr = Prim switch
            {
                Primitive.Int    => "int",
                Primitive.Long   => "long",
                Primitive.Float  => "float",
                Primitive.Double => "double",
                Primitive.Bool   => "bool",
                Primitive.String => "string",
                Primitive.Enum   => EnumName,
                _                => "object",
            };
            return IsArray ? baseStr + "[]" : baseStr;
        }
    }

    /// <summary>
    /// 엑셀 타입 문자열을 FieldType 으로 변환.
    ///   int, long, float, double, bool, string
    ///   enum:eItem, enum[]:ePathType
    ///   int[], string[]
    /// </summary>
    public static class TypeMapper
    {
        public static FieldType Parse(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            var ft = new FieldType();

            if (s.StartsWith("enum"))
            {
                ft.Prim = Primitive.Enum;
                ft.IsArray = s.StartsWith("enum[]");
                int colon = s.IndexOf(':');
                ft.EnumName = colon >= 0 ? s.Substring(colon + 1).Trim() : "object";
                return ft;
            }

            if (s.EndsWith("[]"))
            {
                ft.IsArray = true;
                s = s.Substring(0, s.Length - 2);
            }

            ft.Prim = s.ToLowerInvariant() switch
            {
                "int"    => Primitive.Int,
                "long"   => Primitive.Long,
                "float"  => Primitive.Float,
                "double" => Primitive.Double,
                "bool"   => Primitive.Bool,
                "string" => Primitive.String,
                _        => Primitive.String,  // 알 수 없으면 string 으로 폴백
            };
            return ft;
        }
    }
}
