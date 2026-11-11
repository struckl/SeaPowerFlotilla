using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flotilla
{
    /// <summary>
    /// The game's own mod version rule (SeaPower.ModCompatibility.Matches, the same in 0.8.7 and 0.9.0) for the [Compatibility]
    /// section of a mod's _info.ini. Same logic as tools/modlib/compatver.py in the mods workspace.
    /// </summary>
    public static class Compat
    {
        static readonly string[] Keys = { "ExactVersion", "ApproximateVersion", "GreaterThanVersion", "GreaterThanEqualToVersion",
                                          "LessThanVersion", "LessThanEqualToVersion", "ExcludeVersions" };
        static readonly Regex SemVer = new Regex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$");

        public static Version Parse(string text)
        {
            var m = SemVer.Match((text ?? "").Trim());
            return m.Success ? new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)) : null;
        }

        /// <summary>The [Compatibility] keys of an _info.ini (only the ones the game reads, with a value).</summary>
        public static Dictionary<string, string> Section(string iniText)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool inside = false;
            foreach (var raw in (iniText ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("[")) inside = string.Equals(line, "[Compatibility]", StringComparison.OrdinalIgnoreCase);
                else if (inside && line.Contains("=") && !line.StartsWith(";"))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    var key = Keys.FirstOrDefault(k => string.Equals(k, parts[0].Trim(), StringComparison.OrdinalIgnoreCase));
                    if (key != null && parts[1].Trim().Length > 0) values[key] = parts[1].Trim();
                }
            }
            return values;
        }

        static string Get(Dictionary<string, string> d, string key) => d != null && d.TryGetValue(key, out var v) ? v : null;

        /// <summary>True when the game at version <paramref name="game"/> accepts the declaration, as the game decides it.</summary>
        public static bool Matches(Dictionary<string, string> decl, string game)
        {
            var a = Parse(game);
            if (a == null || decl == null || decl.Count == 0) return true;
            if (Get(decl, "ExactVersion") != null) { var v = Parse(Get(decl, "ExactVersion")); return v != null && v == a; }
            if (Get(decl, "ApproximateVersion") != null)
            {
                var v = Parse(Get(decl, "ApproximateVersion"));
                return v != null && a.Major == v.Major && a.Minor == v.Minor && a.Build >= v.Build;
            }
            foreach (var x in (Get(decl, "ExcludeVersions") ?? "").Split(','))
                if (Parse(x) == a) return false;
            foreach (var (key, ok) in new (string, Func<Version, bool>)[] { ("GreaterThanVersion", v => a > v), ("GreaterThanEqualToVersion", v => a >= v) })
                if (Get(decl, key) != null) { var v = Parse(Get(decl, key)); if (v == null || !ok(v)) return false; break; }
            foreach (var (key, ok) in new (string, Func<Version, bool>)[] { ("LessThanVersion", v => a < v), ("LessThanEqualToVersion", v => a <= v) })
                if (Get(decl, key) != null) { var v = Parse(Get(decl, key)); if (v == null || !ok(v)) return false; break; }
            return true;
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
