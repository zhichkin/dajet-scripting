using DaJet.Data;
using DaJet.Scripting.Model;
using System.Text.RegularExpressions;

namespace DaJet.Scripting
{
    public abstract partial class ScriptContext
    {
        public CancellationToken Cancellation { get; set; }
        public bool IsCancellationRequested { get { return Cancellation.IsCancellationRequested; } }
        public abstract DataSourceScope GetDataSource();
        public abstract object GetValue(in string name);
        public abstract void SetValue(in string name, in object value);
        public abstract bool CreateVariable(in string name);
        public abstract void RemoveVariable(in string name);
        public abstract object Evaluate(in SyntaxNode expression);
        public abstract ExitCode Callback(in StatementBlock statements);

        [GeneratedRegex("{(.*?)}", RegexOptions.CultureInvariant)]
        private static partial Regex TemplateVariableRegex();
        private static readonly Regex _uri_template = TemplateVariableRegex();
        public static string[] GetUriTemplates(in string uri)
        {
            MatchCollection matches = _uri_template.Matches(uri);

            if (matches.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] templates = new string[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                templates[i] = matches[i].Value;
            }

            return templates;
        }
        public string ReplaceUriTemplates(in string uri, in string[] templates)
        {
            string result = uri;

            for (int i = 0; i < templates.Length; i++)
            {
                string variable = templates[i].TrimStart('{').TrimEnd('}');

                object value = GetValue(in variable);

                result = result.Replace(templates[i], value.ToString());
            }

            return result;
        }
        public Uri GetUri(in string uri)
        {
            string[] templates = GetUriTemplates(in uri);

            if (templates.Length == 0)
            {
                return new Uri(uri);
            }

            string result = ReplaceUriTemplates(in uri, in templates);

            return new Uri(result);
        }
    }
}