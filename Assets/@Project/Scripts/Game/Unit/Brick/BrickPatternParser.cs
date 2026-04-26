using SpecData;
using UnityEngine;

// SpecWave.pattern (string[8]) → Brick spawn dispatch.
// Cell notation (Schema/wave.md §pattern 셀 표기법):
//   "."     empty
//   "N"     NORMAL (HP 1)
//   "N(2)"  NORMAL (HP 2)
//   "S(F)"  SHIELD element=FIRE
//   "S(I)"  SHIELD element=ICE
//   "S(E)"  SHIELD element=SHOCK
//   "E"     EXPLOSIVE
//   "P"     SPAWNER
//   "R"     REFLECTOR
//   "B[id]" BOSS (id references boss spec)
public static class BrickPatternParser
{
    public const int COLS = 8;

    public readonly struct ParsedCell
    {
        public readonly bool       isEmpty;
        public readonly eBrickType type;
        public readonly eElement   element;
        public readonly int        hp;       // 0 = use default
        public readonly int        bossId;   // 0 = not boss
        public ParsedCell(bool empty, eBrickType t, eElement e, int hp, int bossId)
        {
            isEmpty = empty; type = t; element = e; this.hp = hp; this.bossId = bossId;
        }
    }

    public static ParsedCell Parse(string cell)
    {
        if (string.IsNullOrEmpty(cell) || cell == ".") return new ParsedCell(true, eBrickType.NORMAL, eElement.NONE, 0, 0);

        int paren = cell.IndexOf('(');
        int bracket = cell.IndexOf('[');
        string head = (paren >= 0) ? cell.Substring(0, paren) : (bracket >= 0) ? cell.Substring(0, bracket) : cell;

        switch (head)
        {
            case "N":
            {
                int hp = ExtractInt(cell, paren);
                return new ParsedCell(false, eBrickType.NORMAL, eElement.NONE, hp, 0);
            }
            case "S":
            {
                var elem = ExtractElement(cell, paren);
                return new ParsedCell(false, eBrickType.SHIELD, elem, 0, 0);
            }
            case "E": return new ParsedCell(false, eBrickType.EXPLOSIVE, eElement.NONE, 0, 0);
            case "P": return new ParsedCell(false, eBrickType.SPAWNER,   eElement.NONE, 0, 0);
            case "R": return new ParsedCell(false, eBrickType.REFLECTOR, eElement.NONE, 0, 0);
            case "B":
            {
                int bossId = ExtractInt(cell, bracket);
                return new ParsedCell(false, eBrickType.BOSS, eElement.NONE, 0, bossId);
            }
            default:
                Debug.LogError($"[BrickPatternParser] unknown cell '{cell}'");
                return new ParsedCell(true, eBrickType.NORMAL, eElement.NONE, 0, 0);
        }
    }

    static int ExtractInt(string cell, int openIdx)
    {
        if (openIdx < 0) return 0;
        int close = cell.IndexOfAny(new[] { ')', ']' }, openIdx + 1);
        if (close <= openIdx) return 0;
        var s = cell.Substring(openIdx + 1, close - openIdx - 1);
        return int.TryParse(s, out var v) ? v : 0;
    }

    static eElement ExtractElement(string cell, int openIdx)
    {
        if (openIdx < 0) return eElement.NONE;
        int close = cell.IndexOf(')', openIdx + 1);
        if (close <= openIdx) return eElement.NONE;
        var tag = cell.Substring(openIdx + 1, close - openIdx - 1);
        return tag switch
        {
            "F" => eElement.FIRE,
            "I" => eElement.ICE,
            "E" => eElement.SHOCK,
            _   => eElement.NONE,
        };
    }
}
