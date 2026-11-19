using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flotilla
{
    /// <summary>
    /// The game's own mod version rule for the [Compatibility] section of a mod's _info.ini, ported from the game (build 26712472):
    /// SeaPower.ModCompatibility.Matches with SeaPower.SemanticVersion, and the section read the way SeaPower.IniHandler reads an
    /// ini file. Kept in this one file with no other Flotilla code, so it can be swapped for a shared copy of the rule.
    /// Not ported: the IniHandler directives (#!copy, #!extend, #!repeat), which no _info.ini uses.
    /// </summary>
    public static class Compat
    {
        const string SectionName = "Compatibility";
        static readonly string[] Keys = { "ExactVersion", "ApproximateVersion", "GreaterThanVersion", "GreaterThanEqualToVersion",
                                          "LessThanVersion", "LessThanEqualToVersion", "ExcludeVersions" };

        // The game's SemanticVersion pattern, unchanged: a pre-release part must follow the semver rules too.
        static readonly Regex SemVer = new Regex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$");
        static readonly Regex CommentLine = new Regex("^(//|#|;)");
        static readonly string[] CommentMarks = { ";", "//", "#" };
        static readonly Regex Header = new Regex(@"^\[(.+)\]");
        static readonly Regex IgnoredHeader = new Regex(@"^[ =\-*]");

        /// <summary>
        /// Major, minor and patch of a version the game accepts, else null. The game compares only those three numbers. A number
        /// too big for an int throws inside the game; here it is a version the game cannot read.
        /// </summary>
        public static Version Parse(string text)
        {
            var m = SemVer.Match(text ?? "");
            if (!m.Success) return null;
            var parts = new int[3];
            for (int i = 0; i < 3; i++)
                if (!int.TryParse(m.Groups[i + 1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out parts[i])) return null;
            return new Version(parts[0], parts[1], parts[2]);
        }

        /// <summary>The [Compatibility] keys of an _info.ini that the game reads, each with a value.</summary>
        public static Dictionary<string, string> Section(string iniText)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool inside = false;
            foreach (var raw in (iniText ?? "").Split('\n'))
            {
                var line = WithoutComment(raw);
                if (line == null) continue;
                var header = Header.Match(line);
                if (header.Success) inside = Enters(header.Groups[1].Value, seen);
                else if (inside) AddFirst(values, line);
            }
            var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in Keys)
                if (values.TryGetValue(key, out var value) && value.Length > 0) known[key] = value;
            return known;
        }

        /// <summary>The line as the game reads it, without its comment; null for a comment line.</summary>
        static string WithoutComment(string raw)
        {
            var text = raw.Trim();
            if (CommentLine.IsMatch(text)) return null;
            return text.Split(CommentMarks, StringSplitOptions.None)[0].Trim();
        }

        /// <summary>
        /// Whether a section header opens [Compatibility]. Section names are case-sensitive in the game, and a section named a
        /// second time in the same file is skipped.
        /// </summary>
        static bool Enters(string name, HashSet<string> seen)
        {
            if (IgnoredHeader.IsMatch(name) || !seen.Add(name)) return false;
            return name == SectionName;
        }

        /// <summary>The first value of a key wins, an empty one too; parts around further '=' are trimmed and joined.</summary>
        static void AddFirst(Dictionary<string, string> values, string line)
        {
            var parts = line.Split('=');
            if (parts.Length < 2) return;
            var key = parts[0].Trim();
            if (!values.ContainsKey(key)) values.Add(key, string.Join("=", parts.Skip(1).Select(p => p.Trim())));
        }

        static string Get(Dictionary<string, string> d, string key) => d != null && d.TryGetValue(key, out var v) ? v : null;

        /// <summary>True when the game at version <paramref name="game"/> accepts the declaration, as the game decides it.</summary>
        public static bool Matches(Dictionary<string, string> decl, string game)
        {
            var app = Parse(game);
            if (app == null || decl == null || decl.Count == 0) return true;
            var exact = Get(decl, "ExactVersion");
            if (exact != null) return Holds(exact, v => app == v);
            var approx = Get(decl, "ApproximateVersion");
            if (approx != null) return Holds(approx, v => app.Major == v.Major && app.Minor == v.Minor && app.Build >= v.Build);
            return !Excluded(decl, app) && Lower(decl, app) && Upper(decl, app);
        }

        /// <summary>A bound the game cannot read is a "no" in the game.</summary>
        static bool Holds(string bound, Func<Version, bool> ok)
        {
            var v = Parse(bound);
            return v != null && ok(v);
        }

        /// <summary>Only readable entries count, and each one is trimmed first.</summary>
        static bool Excluded(Dictionary<string, string> decl, Version app) =>
            (Get(decl, "ExcludeVersions") ?? "").Split(',').Any(x => Parse(x.Trim()) == app);

        /// <summary>GreaterThanVersion wins over GreaterThanEqualToVersion when both are set.</summary>
        static bool Lower(Dictionary<string, string> decl, Version app)
        {
            var above = Get(decl, "GreaterThanVersion");
            if (above != null) return Holds(above, v => app > v);
            var from = Get(decl, "GreaterThanEqualToVersion");
            return from == null || Holds(from, v => app >= v);
        }

        /// <summary>LessThanVersion wins over LessThanEqualToVersion when both are set.</summary>
        static bool Upper(Dictionary<string, string> decl, Version app)
        {
            var below = Get(decl, "LessThanVersion");
            if (below != null) return Holds(below, v => app < v);
            var upTo = Get(decl, "LessThanEqualToVersion");
            return upTo == null || Holds(upTo, v => app <= v);
        }

        /// <summary>Short words for the Mods list and the check: "0.8.5", "0.8.7 up to 0.10.0", "any".</summary>
        public static string Describe(Dictionary<string, string> decl)
        {
            if (decl == null || decl.Count == 0) return "any";
            if (Get(decl, "ExactVersion") != null) return "only " + Get(decl, "ExactVersion");
            if (Get(decl, "ApproximateVersion") != null) return Get(decl, "ApproximateVersion");
            var low = Get(decl, "GreaterThanEqualToVersion") ?? (Get(decl, "GreaterThanVersion") != null ? "above " + Get(decl, "GreaterThanVersion") : null);
            var high = Get(decl, "LessThanVersion") != null ? "up to " + Get(decl, "LessThanVersion")
                     : Get(decl, "LessThanEqualToVersion") != null ? "up to and with " + Get(decl, "LessThanEqualToVersion") : null;
            var text = string.Join(" ", new[] { low, high }.Where(s => s != null));
            return text.Length == 0 ? "any" : text;
        }
    }
}
