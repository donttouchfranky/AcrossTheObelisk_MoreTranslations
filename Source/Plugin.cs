using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace MoreTranslations
{
    [BepInPlugin("MoreTranslations_DontTouchFranky", "MoreTranslations", "1.1.2")]
    public class Plugin : BaseUnityPlugin
    {
        private static Dictionary<string, Dictionary<string, string>> TextStrings;
        private static Dictionary<string, Dictionary<string, string>> TextKeynotes;
        private static TMP_Dropdown languagesDropdown = null;
        private static string selectedLanguage = null;
        private static List<String> languages = new List<String>();
        private static List<String> tips = new List<String>();
        private static TMP_FontAsset alternativeFont = null;

        void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        /*[HarmonyPatch(typeof(AtOManager), "Update"), HarmonyPrefix]
        static void Update()
        {
            // Utilizzo un font diverso in caso di caratteri speciali
            if (alternativeFont != null)
            {
                List<TMP_Text> texts = new List<TMP_Text>(FindObjectsOfType<TMP_Text>());
                for (int i = 0; i < texts.Count; i++)
                {
                    Debug.Log("i: " + i + "   outlineColor: " + texts[i].outlineColor.ToString() + "   outlineWidth: " + texts[i].outlineWidth);
                    if (forceFont)
                        texts[i].font = alternativeFont;
                    else
                        texts[i].font.fallbackFontAssetTable = new List<TMP_FontAsset>() { alternativeFont };
                    Debug.Log("j: " + i + "   outlineColor: " + texts[i].outlineColor.ToString() + "   outlineWidth: " + texts[i].outlineWidth);
                }
            }
        }*/

        // switched to OnEnable - it's faster and less laggy
        [HarmonyPatch(typeof(TextMeshPro), "OnEnable"), HarmonyPostfix]
        public static void FontPatchTMPText(TextMeshPro __instance)
        {
            // Utilizzo un font diverso in caso di caratteri speciali
            if (alternativeFont != null)
                __instance.font.fallbackFontAssetTable = new List<TMP_FontAsset>() { alternativeFont };
        }

        [HarmonyPatch(typeof(GameManager), "Start"), HarmonyPrefix]
        static void Start()
        {
            // Carico il font CantoraOne-Regular Fix SDF.asset dalla cartella attuale
            string thisPath = Paths.PluginPath; // use bepinex path. fonts are expected to be in the Plugins folder along with the dll
            selectedLanguage = PlayerPrefs.GetString("linguaSelezionata");
            string filePath = thisPath;
            if (selectedLanguage.ToLower() == "jp" || selectedLanguage.ToLower() == "japanese" || selectedLanguage.ToLower() == "russian" || selectedLanguage.ToLower() == "ru") // added font for jp lang
                filePath += "/NotoSerifJP-Regular.otf";
            else
                filePath += "/CantoraOne-Regular Fix.ttf";
            if (File.Exists(filePath))
            {
                Font font = new Font(filePath);
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);

                if (fontAsset != null)
                    alternativeFont = fontAsset;
            }

            TextStrings = new Dictionary<string, Dictionary<string, string>>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
            TextKeynotes = new Dictionary<string, Dictionary<string, string>>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
            if (selectedLanguage != "")
            {
                Debug.Log("MoreTranslations | Selected -> " + selectedLanguage);

                TextStrings[selectedLanguage] = new Dictionary<string, string>();
                TextKeynotes[selectedLanguage] = new Dictionary<string, string>();
            }
            else
            {
                Debug.Log("MoreTranslations | Selected -> Default");
            }

            CreateLanguageDropdown();
            GetTranslationLanguages();

            // Se la lingua selezionata non è presente tra quelle disponibili, la imposto a default
            if (selectedLanguage != "" && !languages.Contains(selectedLanguage))
            {
                selectedLanguage = "";
                PlayerPrefs.SetString("linguaSelezionata", selectedLanguage);
            }

            languagesDropdown.options.Add(new TMP_Dropdown.OptionData("Default"));

            // Aggiungo le lingue al dropdown
            foreach (String lingua in languages)
            {
                languagesDropdown.options.Add(new TMP_Dropdown.OptionData(Capitalize(lingua.ToLower())));
            }

            if (selectedLanguage == "")
            {
                languagesDropdown.value = 0;
            }
            else
            {
                languagesDropdown.value = languagesDropdown.options.FindIndex(x => x.text.ToLower() == selectedLanguage.ToLower());
            }

            languagesDropdown.onValueChanged.AddListener((value) =>
            {
                String selectedLanguage = languagesDropdown.options[value].text;
                if (selectedLanguage == "Default")
                {
                    selectedLanguage = "";
                }

                PlayerPrefs.SetString("linguaSelezionata", selectedLanguage.ToLower());
                AlertManager.Instance.AlertConfirm(Texts.Instance.GetText("selectLanguageChanged"));
            });
        }

        static void CreateLanguageDropdown()
        {
            // Duplico il dropdown delle lingue
            TMP_Dropdown originalDropdown = SettingsManager.Instance.languageDropdown;
            TMP_Dropdown clonedDropdown = Instantiate(originalDropdown, originalDropdown.transform.parent);
            clonedDropdown.transform.SetSiblingIndex(originalDropdown.transform.GetSiblingIndex() + 1);
            clonedDropdown.name = "languageDropdown2";
            clonedDropdown.onValueChanged.RemoveAllListeners();
            clonedDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();

            // Sposto il dropdown delle lingue a destra del primo 
            RectTransform rectTransform = clonedDropdown.GetComponent<RectTransform>();
            double width = rectTransform.rect.width;
            rectTransform.anchoredPosition = new Vector2((float)(rectTransform.anchoredPosition.x + width + 10), rectTransform.anchoredPosition.y);

            // Svuoto il dropdown delle lingue
            clonedDropdown.ClearOptions();

            languagesDropdown = clonedDropdown;
        }

        static void GetTranslationLanguages()
        {
            // Cerco tra tutte le cartelle 
            string[] folders = Directory.GetDirectories(Paths.PluginPath); // use bepinex path
            foreach (string cartella in folders)
            {
                // Check if moretranslations.txt exists in folder
                string moretranslationsPath = Path.Combine(cartella, "moretranslations.txt");
                if (File.Exists(moretranslationsPath))
                {
                    string[] lines = File.ReadAllLines(moretranslationsPath);
                    foreach (string line in lines)
                    {
                        // Check if line is a comment
                        if (line.StartsWith("#"))
                        {
                            continue;
                        }

                        string language = line.Trim().ToLower();
                        if (!languages.Contains(language))
                        {
                            languages.Add(language);
                        }
                    }
                }
            }

            if (languages.Count > 0)
            {
                Debug.Log("MoreTranslations | Languages found:");
                foreach (String lingua in languages)
                {
                    Debug.Log("MoreTranslations | - " + lingua);
                }
            }
            else
            {
                Debug.LogWarning("MoreTranslations | No languages found. Please install a language pack to use this plugin.");
            }
        }

        static String Capitalize(string str)
        {
            if (str == null || str.Length < 1)
                return str;

            return char.ToUpper(str[0]) + str.Substring(1);
        }

        [HarmonyPatch(typeof(Texts), "GetText"), HarmonyPrefix]
        static bool GetTextPrefix(string _id, string _type, ref string __result)
        {
            __result = "";

            if ((UnityEngine.Object)Globals.Instance == (UnityEngine.Object)null || !GameManager.Instance.PrefsLoaded)
            {
                __result = "";
                return false;
            }

            string id = _id.Replace(" ", "").ToLower();
            if (!(id != ""))
            {
                __result = "";
                return false;
            }

            if (TextStrings != null && TextStrings.ContainsKey(selectedLanguage))
            {
                if (_type != "")
                    id = _type.ToLower() + "_" + id.ToLower();

                if (TextStrings[selectedLanguage].ContainsKey(id))
                {
                    string testo = TextStrings[selectedLanguage][id];

                    if (testo != "")
                    {
                        {
                            __result = testo;
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(Texts), "LoadTranslationText"), HarmonyPostfix]
        static void LoadTranslationTextPostfix(string type)
        {
            if (tips.Count > 0)
            {
                Texts.Instance.TipsList.Clear();
                Texts.Instance.TipsList.AddRange(tips);
            }
        }

        [HarmonyPatch(typeof(Texts), "LoadTranslationText"), HarmonyPrefix]
        static void LoadTranslationTextPrefix(string type)
        {
            if (selectedLanguage != "")
            {
                // string thisPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                // Cerco tra tutte le cartelle 
                string pluginsPath = Paths.PluginPath; // use bepinex path
                string translationsPath = "";
                string[] folders = Directory.GetDirectories(pluginsPath);
                foreach (string cartella in folders)
                {
                    // Check if moretranslations.txt exists in folder
                    string moretranslationsPath = cartella + "/moretranslations.txt";
                    if (File.Exists(moretranslationsPath))
                    {
                        string[] linesTmp = File.ReadAllLines(moretranslationsPath);
                        foreach (string line in linesTmp)
                        {
                            // Check if line is a comment
                            if (line.StartsWith("#"))
                            {
                                continue;
                            }

                            string language = line.Trim().ToLower();
                            if (language == selectedLanguage)
                            {
                                translationsPath = cartella + "/";
                            }
                        }
                    }
                }

                if (translationsPath != "")
                {
                    string path = "";
                    string[] lines = null;
                    //type = type.ToLower();
                    switch (type.ToLower())
                    {
                        case "":
                            path = translationsPath + "/" + selectedLanguage + ".txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "keynotes":
                            path = translationsPath + "/" + selectedLanguage + "_keynotes.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "traits":
                            path = translationsPath + "/" + selectedLanguage + "_traits.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "auracurse":
                            path = translationsPath + "/" + selectedLanguage + "_auracurse.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "events":
                            path = translationsPath + "/" + selectedLanguage + "_events.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "nodes":
                            path = translationsPath + "/" + selectedLanguage + "_nodes.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "cards":
                            path = translationsPath + "/" + selectedLanguage + "_cards.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "fluff":
                            path = translationsPath + "/" + selectedLanguage + "_cardsfluff.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "class":
                            path = translationsPath + "/" + selectedLanguage + "_class.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "monsters":
                            path = translationsPath + "/" + selectedLanguage + "_monsters.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "requirements":
                            path = translationsPath + "/" + selectedLanguage + "_requirements.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                        case "tips":
                            path = translationsPath + "/" + selectedLanguage + "_tips.txt";
                            if (File.Exists(path))
                                lines = File.ReadAllLines(path);
                            break;
                    }

                    if (lines != null)
                    {
                        List<string> stringList = new List<string>(lines);

                        int num = 0;
                        StringBuilder stringBuilder1 = new StringBuilder();
                        StringBuilder stringBuilder2 = new StringBuilder();

                        for (int index = 0; index < stringList.Count; ++index)
                        {
                            string str2 = stringList[index];
                            if (!(str2 == "") && str2[0] != '#')
                            {
                                string[] strArray = str2.Trim().Split(new char[1] { '=' }, 2);

                                if (strArray != null && strArray.Length >= 2)
                                {
                                    strArray[0] = strArray[0].Trim().ToLower();
                                    strArray[1] = Functions.SplitString("//", strArray[1])[0].Trim();
                                    switch (type.ToLower())
                                    {
                                        case "keynotes":
                                            stringBuilder1.Append("keynotes_");
                                            break;
                                        case "traits":
                                            stringBuilder1.Append("traits_");
                                            break;
                                        case "auracurse":
                                            stringBuilder1.Append("auracurse_");
                                            break;
                                        case "events":
                                            stringBuilder1.Append("events_");
                                            break;
                                        case "nodes":
                                            stringBuilder1.Append("nodes_");
                                            break;
                                        case "cards":
                                        case "fluff":
                                            stringBuilder1.Append("cards_");
                                            break;
                                        case "class":
                                            stringBuilder1.Append("class_");
                                            break;
                                        case "monsters":
                                            stringBuilder1.Append("monsters_");
                                            break;
                                        case "requirements":
                                            stringBuilder1.Append("requirements_");
                                            break;
                                        case "tips":
                                            stringBuilder1.Append("tips_");
                                            break;
                                    }

                                    stringBuilder1.Append(strArray[0]);

                                    if (TextStrings[selectedLanguage].ContainsKey(stringBuilder1.ToString()))
                                        TextStrings[selectedLanguage][stringBuilder1.ToString()] = strArray[1];
                                    else
                                        TextStrings[selectedLanguage].Add(stringBuilder1.ToString(), strArray[1]);

                                    if (type.ToLower() == "tips")
                                    {
                                        tips.Add(strArray[1]);
                                    }

                                    bool flag = true;
                                    if (type == "")
                                    {
                                        if (strArray[1].StartsWith("rptd_", StringComparison.OrdinalIgnoreCase))
                                        {
                                            stringBuilder2.Append(strArray[1].Substring(5).ToLower());
                                            TextStrings[selectedLanguage][stringBuilder1.ToString()] = TextStrings[selectedLanguage][stringBuilder2.ToString()];
                                            flag = false;
                                            stringBuilder2.Clear();
                                        }
                                    }
                                    else if (type.ToLower() == "events")
                                    {
                                        if (strArray[1].StartsWith("rptd_", StringComparison.OrdinalIgnoreCase))
                                        {
                                            stringBuilder2.Append("events_");
                                            stringBuilder2.Append(strArray[1].Substring(5).ToLower());
                                            TextStrings[selectedLanguage][stringBuilder1.ToString()] = TextStrings[selectedLanguage][stringBuilder2.ToString()];
                                            flag = false;
                                            stringBuilder2.Clear();
                                        }
                                    }
                                    else if (type.ToLower() == "cards")
                                    {
                                        if (strArray[1].StartsWith("rptd_", StringComparison.OrdinalIgnoreCase))
                                        {
                                            stringBuilder2.Append("cards_");
                                            stringBuilder2.Append(strArray[1].Substring(5).ToLower());
                                            TextStrings[selectedLanguage][stringBuilder1.ToString()] = TextStrings[selectedLanguage][stringBuilder2.ToString()];
                                            flag = false;
                                            stringBuilder2.Clear();
                                        }
                                    }
                                    else if (type.ToLower() == "monsters" && strArray[1].StartsWith("rptd_", StringComparison.OrdinalIgnoreCase))
                                    {
                                        stringBuilder2.Append("monsters_");
                                        stringBuilder2.Append(strArray[1].Substring(5).ToLower());
                                        TextStrings[selectedLanguage][stringBuilder1.ToString()] = TextStrings[selectedLanguage][stringBuilder2.ToString()];
                                        flag = false;
                                        stringBuilder2.Clear();
                                    }

                                    if (flag)
                                    {
                                        string str3 = Regex.Replace(Regex.Replace(strArray[1], "<(.*?)>", ""), "\\s+", " ");
                                        num += str3.Split(' ').Length;
                                    }
                                    stringBuilder1.Clear();
                                }
                            }
                        }
                    }
                }
            }
        }
        public static void ExportTextForTranslation() // for when vanilla texts are updated
        {
            ExportLanguage("en");
            ExportLanguage("zh-CN");
            ExportLanguage("es");
            ExportLanguage("ko");
            ExportLanguage("sv");
        }
        public static void ExportLanguage(string lang)
        {
            DirectoryInfo medsDI = new DirectoryInfo(Path.Combine(Paths.PluginPath, "MoreTranslations_" + LangShortToLong(lang)));
            if (!medsDI.Exists)
                medsDI.Create();
            ActualExport(lang, lang);
            ActualExport(lang + "_keynotes", lang);
            ActualExport(lang + "_traits", lang);
            ActualExport(lang + "_auracurse", lang);
            ActualExport(lang + "_events", lang);
            ActualExport(lang + "_nodes", lang);
            ActualExport(lang + "_cards", lang);
            ActualExport(lang + "_cardsfluff", lang);
            ActualExport(lang + "_class", lang);
            ActualExport(lang + "_monsters", lang);
            ActualExport(lang + "_requirements", lang);
            ActualExport(lang + "_tips", lang);
            File.WriteAllText(Path.Combine(Paths.PluginPath, "MoreTranslations_" + LangShortToLong(lang), "moretranslations.txt"), LangShortToLong(lang));
        }

        public static void ActualExport(string fileName, string lang)
        {
            Debug.Log("Exporting " + fileName);
            UnityEngine.TextAsset textAsset = Resources.Load("Lang/" + lang + "/" + fileName) as UnityEngine.TextAsset;
            string writeName = fileName.Replace(lang + "_", LangShortToLong(lang) + "_");
            writeName = LangShortToLong(writeName);
            if (textAsset == null)
            {
                Debug.Log("textassetnull: " + fileName);
                return;
            }
            File.WriteAllText(Path.Combine(Paths.PluginPath, "MoreTranslations_" + LangShortToLong(lang), writeName + ".txt"), textAsset.ToString());
        }
        public static string LangShortToLong(string lang)
        {
            string longLang = lang;
            if (lang == "en")
                longLang = "English";
            if (lang == "zh-CN")
                longLang = "简体中文";
            if (lang == "es")
                longLang = "español";
            if (lang == "ko")
                longLang = "한국인";
            if (lang == "sv")
                longLang = "svenska";
            return longLang;
        }
    }
}
