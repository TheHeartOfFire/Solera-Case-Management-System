using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Text;
using System.Xml;
using System.IO;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class DotFormgen : INotifyPropertyChanged, IEquatable<DotFormgen>
    {
        [NotifyPropertyChanged]
        private DotFormgenSettings _settings;

        [NotifyPropertyChanged]
        private List<FormPage> _pages = new();

        [NotifyPropertyChanged]
        private string? _title;

        [NotifyPropertyChanged]
        private bool _tradePrompt;

        [NotifyPropertyChanged]
        private Format _formType;

        [NotifyPropertyChanged]
        private bool _salesPersonPrompt;

        [NotifyPropertyChanged]
        private string? _username;

        [NotifyPropertyChanged]
        private string? _billingName;

        [NotifyPropertyChanged]
        private List<CodeLine> _codeLines = new();

        [NotifyPropertyChanged]
        private FormCategory _category;

        [NotifyPropertyChanged]
        private List<string> _states = new();

        public enum Format
        {
            Impact,
            ImpactLabelRoll,
            ImpactLabelSheet,
            LaserLabelSheet,
            LegacyImpact,
            LegacyLaser,
            Pdf
        }

        public enum FormCategory
        {
            Aftermarket,
            BuyersGuide,
            Commission,
            CreditLifeAH,
            Custom,
            DealRecap,
            EnvelopeDealJacket,
            ExtendedWarranties,
            Gap,
            Insurance,
            Label,
            Lease,
            Maintenance,
            MemberApplication,
            NoticeToCosigner,
            NoticeToCustomer,
            Other,
            PurchaseOrderInvoice,
            RebateIncentive,
            Retail,
            StateSpecificDMV,
            WeOweYouOweDueBill
        }

        public DotFormgen()
        {
            Settings = new DotFormgenSettings();
            Settings.PropertyChanged += (s, e) => OnPropertyChanged();
        }

        public DotFormgen(XmlElement document)
        {
            Settings = new DotFormgenSettings(document.Attributes);
            Settings.PropertyChanged += (s, e) => OnPropertyChanged();
            foreach (XmlNode node in document.ChildNodes)
            {
                switch (node.Name)
                {
                    case "pages": Pages.Add(new FormPage(node));
                        break;
                    case "title": Title = node.InnerText;
                        break;
                    case "tradePrompt":
                        if (bool.TryParse(node.InnerText, out var parsedBool))
                            TradePrompt = parsedBool;
                        break;
                    case "formPrintType": FormType = GetFormat(node.InnerText);
                        break;
                    case "salespersonPrompt":
                        if (bool.TryParse(node.InnerText, out parsedBool))
                            SalesPersonPrompt = parsedBool;
                        break;
                    case "username": Username = node.InnerText;
                        break;
                    case "billingName": BillingName = node.InnerText;
                        break;
                    case "codeLines": CodeLines.Add(new CodeLine(node));
                        break;
                    case "formCategory": Category = GetCategory(node.InnerText);
                        break;
                    case "validStates":
                        States.Add(node.InnerText);
                        break;
                }
            }
            foreach (var page in Pages) page.PropertyChanged += (s, e) => OnPropertyChanged();
            foreach (var line in CodeLines) line.PropertyChanged += (s, e) => OnPropertyChanged();

        }

        public CodeLine GetPrompt(int index)
        {
            var prompts = CodeLines.Where(x => x.Settings is {Type: CodeLineSettings.CodeType.PROMPT}).ToList();

            return prompts[index];
        }

        public FormField GetField(int index)
        {
            var fields = new List<FormField>();
            foreach(var page in Pages) fields.AddRange(page.Fields);

            return fields[index];
        }
        public int FieldCount()
        {
            var count = 0;
            foreach (var page in Pages)
                foreach (var field in page.Fields)
                    count++;

            return count;
        }
        public int InitCount()
        {
            return CodeLines.Count(x => x.Settings is {Type: CodeLineSettings.CodeType.INIT});
        }
        public int PromptCount()
        {
            return CodeLines.Count(x => x.Settings is {Type: CodeLineSettings.CodeType.PROMPT});
        }
        public int PostCount()
        {
            return CodeLines.Count(x => x.Settings is {Type: CodeLineSettings.CodeType.POST});
        }
        public CodeLine ClonePrompt(CodeLine prompt, string? newName, int newIndex)
        {
            var cl = new CodeLine(prompt, newName, newIndex);
            CodeLines.Add(cl);
            return cl;
        }
        public static Format GetFormat(string format) => format switch
        {
            "Pdf" => Format.Pdf,
            "LegacyImpact" => Format.LegacyImpact,
            _ => Format.Pdf,
        }; 
        
        public static string GetFormat(Format format) => format switch
        {
            Format.Pdf => "Pdf",
            Format.LegacyImpact => "LegacyImpact",
            _ => "Pdf",
        };

        public static FormCategory GetCategory(string category) => category switch
        {
            "Aftermarket" => FormCategory.Aftermarket,
            "BuyersGuide" => FormCategory.BuyersGuide,
            "Commission" => FormCategory.Commission,
            "CreditLifeAH" => FormCategory.CreditLifeAH,
            "Custom" => FormCategory.Custom,
            "DealRecap" => FormCategory.DealRecap,
            "EnvelopeDealJacket" => FormCategory.EnvelopeDealJacket,
            "ExtendedWarranties" => FormCategory.ExtendedWarranties,
            "Gap" => FormCategory.Gap,
            "Insurance" => FormCategory.Insurance,
            "Label" => FormCategory.Label,
            "Lease" => FormCategory.Lease,
            "Maintenance" => FormCategory.Maintenance,
            "MemberApplication" => FormCategory.MemberApplication,
            "NoticeToCoSigner" => FormCategory.NoticeToCosigner,
            "NoticeToCustomer" => FormCategory.NoticeToCustomer,
            "Other" => FormCategory.Other,
            "PurchaseOrderInvoice" => FormCategory.PurchaseOrderInvoice,
            "RebateIncentive" => FormCategory.RebateIncentive,
            "Retail" => FormCategory.Retail,
            "StateSpecificDMV" => FormCategory.StateSpecificDMV,
            "WeOweYouOweDueBill" => FormCategory.WeOweYouOweDueBill,
            _ => FormCategory.Other,

        };

        public static string GetCategory(FormCategory category) => category switch
        {
            FormCategory.Aftermarket => "Aftermarket",
            FormCategory.BuyersGuide => "BuyersGuide",
            FormCategory.Commission => "Commission",
            FormCategory.CreditLifeAH => "CreditLifeAH",
            FormCategory.Custom => "Custom",
            FormCategory.DealRecap => "DealRecap",
            FormCategory.EnvelopeDealJacket => "EnvelopeDealJacket",
            FormCategory.ExtendedWarranties => "ExtendedWarranties",
            FormCategory.Gap => "Gap",
            FormCategory.Insurance => "Insurance",
            FormCategory.Label => "Label",
            FormCategory.Lease => "Lease",
            FormCategory.Maintenance => "Maintenance",
            FormCategory.MemberApplication => "MemberApplication",
            FormCategory.NoticeToCosigner => "NoticeToCoSigner",
            FormCategory.NoticeToCustomer => "NoticeToCustomer",
            FormCategory.Other => "Other",
            FormCategory.PurchaseOrderInvoice => "PurchaseOrderInvoice",
            FormCategory.RebateIncentive => "RebateIncentive",
            FormCategory.Retail => "Retail",
            FormCategory.StateSpecificDMV => "StateSpecificDMV",
            FormCategory.WeOweYouOweDueBill => "WeOweYouOweDueBill",
            _ => "Other",

        };

        public string GenerateXML()
        {
            var output = new StringBuilder();
            var sw = new StringWriterWithEncoding(output, Encoding.UTF8);
            var xml = XmlWriter.Create(sw, new XmlWriterSettings() { Indent = true});
            
            xml.WriteStartDocument(true);

            xml.WriteStartElement("formDef");
            Settings.GenerateXML(xml);

            foreach (var page in Pages)
                page.GenerateXml(xml);

            xml.WriteStartElement("title");
            xml.WriteString(Title);
            xml.WriteEndElement();

            xml.WriteStartElement("tradePrompt");
            xml.WriteString(TradePrompt.ToString().ToLowerInvariant());
            xml.WriteEndElement();

            xml.WriteStartElement("formPrintType");
            xml.WriteString(GetFormat(FormType));
            xml.WriteEndElement();

            xml.WriteStartElement("salespersonPrompt");
            xml.WriteString(SalesPersonPrompt.ToString().ToLowerInvariant());
            xml.WriteEndElement();

            xml.WriteStartElement("username");
            xml.WriteString(Username);
            xml.WriteEndElement();

            if (BillingName != null)
            {
                xml.WriteStartElement("billingName");
                xml.WriteString(BillingName);
                xml.WriteEndElement();
            }

            foreach (var line in CodeLines.Where(line => line.Settings is {Type: CodeLineSettings.CodeType.INIT}))
            {
                line.GenerateXml(xml);
            }

            foreach (var line in CodeLines.Where(line => line.Settings is {Type: CodeLineSettings.CodeType.PROMPT}))
            {
                line.GenerateXml(xml);
            }

            foreach (var line in CodeLines.Where(line => line.Settings is {Type: CodeLineSettings.CodeType.POST}))
            {
                line.GenerateXml(xml);
            }


            xml.WriteStartElement("formCategory");
            xml.WriteString(GetCategory(Category));
            xml.WriteEndElement();

            foreach (var state in States)
            {
                xml.WriteStartElement("validStates");
                xml.WriteString(state);
                xml.WriteEndElement();
            }

            xml.WriteEndDocument();
            xml.Close();
            return output.ToString();
        }

        public DotFormgen Clone()
        {
            // Serialize to XML and deserialize to a new instance for a deep copy
            var xml = this.GenerateXML();
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            return new DotFormgen(xmlDoc.DocumentElement!);
        }

        public bool Equals(DotFormgen? other) =>
            other is not null &&
            Settings.Equals(other.Settings) &&
            Pages.SequenceEqual(other.Pages) &&
            (Title?.Equals(other.Title) ?? false) &&
            TradePrompt == other.TradePrompt &&
            FormType == other.FormType &&
            SalesPersonPrompt == other.SalesPersonPrompt &&
            (Username?.Equals(other.Username) ?? false) &&
            (BillingName?.Equals(other.BillingName) ?? false) &&
            CodeLines.SequenceEqual(other.CodeLines) &&
            Category == other.Category &&
            States.SequenceEqual(other.States);

        public override bool Equals(object? obj) => Equals(obj as DotFormgen);
        public override int GetHashCode() => HashCode.Combine(Settings, Pages, Title, TradePrompt, FormType, SalesPersonPrompt, 
            HashCode.Combine(Username, BillingName, CodeLines, Category, States));
    }
    public class StringWriterWithEncoding(StringBuilder sb, Encoding encoding) : StringWriter(sb)
    {
        public override Encoding Encoding { get; } = encoding;
    }
}
