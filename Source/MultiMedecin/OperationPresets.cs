using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace MultiDoctorSurgery
{
    public static class OperationPresets
    {
        public static bool IsValidName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length <= 80 &&
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.Contains("..") &&
                name == name.Trim() && !name.EndsWith(".");
        }

        private static string FilePath(string name)
        {
            if (!IsValidName(name)) throw new ArgumentException("Invalid preset name.");
            return Path.Combine(GenFilePaths.ConfigFolderPath, $"ExcludedOperations_{name}.xml");
        }

        public static bool Save(string name, IEnumerable<string> operations)
        {
            string temporary = null;
            try
            {
                var path = FilePath(name);
                temporary = path + ".tmp";
                var entries = (operations ?? Enumerable.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
                // Same file shape as Scribe, without changing the game's global save/load state.
                new XDocument(new XElement("ExcludedOperations", new XElement("excludedOperations",
                    entries.Select(s => new XElement("li", s))))).Save(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Report(ex);
                return false;
            }
            finally
            {
                if (temporary != null && File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public static bool Load(string name, out List<string> operations)
        {
            operations = null;
            try
            {
                using (var reader = XmlReader.Create(FilePath(name), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                {
                    var list = XDocument.Load(reader).Element("ExcludedOperations")?.Element("excludedOperations");
                    if (list == null) throw new XmlException("Missing excludedOperations element.");
                    operations = list.Elements("li").Select(e => e.Value)
                        .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is XmlException)
            {
                Report(ex);
                return false;
            }
        }

        private static void Report(Exception exception)
        {
            Log.Warning("[MultiDoctorSurgery] Preset error: " + exception);
            Messages.Message("Preset_Error".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}
