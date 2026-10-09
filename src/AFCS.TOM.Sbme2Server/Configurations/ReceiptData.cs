namespace AFCS.TOM.Sbme2Server.Configurations
{
    public sealed class ReceiptData
    {
        public string DeviceClass { get; set; } = "DSDE";
        public string CompanyName { get; set; } = string.Empty;

        [System.Xml.Serialization.XmlIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        [Obsolete("Use the GetCompanyNameAllignedToCenter() if the receipt line max length is known")]
        public string CompanyNameFormatted
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CompanyName)) return string.Empty;
                var result = CompanyName;
                var posL = result.IndexOf('~');
                var posR = result.IndexOf('~', posL + 1);
                while (posL >= 0 && posR >= 0)
                {
                    var value = result.Substring(posL, posR - posL + 1).Replace("~", string.Empty);
                    result = result.Remove(posL, posR - posL + 1);

                    var newStart = 0;
                    if (value.ToLower() == "l")
                    {
                        result = result.Insert(posL, "\r\n");
                        newStart = posL + 2;
                    }
                    else if (int.TryParse(value, out var number))
                    {
                        var spaces = string.Empty.PadLeft(number, ' ');
                        result = result.Insert(posL, spaces);
                        newStart = posL + number;
                    }
                    posL = result.IndexOf('~', newStart);
                    posR = result.IndexOf('~', posL + 1);
                }
                return result;
            }
        }

        public string[]? GetCompanyNameAllignedToCenter(int lineMaxLength)
        {
            if (string.IsNullOrWhiteSpace(CompanyName)) return null;
            CompanyName = CompanyName.Replace("\\n", "\n").Replace("\\r", "\r");
            var split = CompanyName.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0) return null;
            return split.Select(p => GetStrinAllignToCenter(p, lineMaxLength)).ToArray();
        }

        public static string GetStrinAllignToCenter(string text, int lineMaxLine)
        {
            const string space_tag = "~SPC~";
            var value = text.Trim();
            if (value.ToUpper().Equals(space_tag)) return string.Empty;
            var dif = lineMaxLine - value.Length;
            if (dif <= 0) return value;
            if (dif % 2 != 0)
            {
                var spaces = value.Count(p => p == ' ');
                if (spaces % 2 == 1 && dif - spaces >= 0)
                {
                    value = value.Replace(" ", "  ");
                    dif -= spaces;
                }
            }
            if (dif <= 0) return value;
            dif = dif >> 1;
            return value.PadLeft(value.Length + dif, ' ');
        }
    }
}
