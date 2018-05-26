using JsonParser.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DevPal
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        enum InputTypes
        {
            Empty,
            PythonArgs,
            CommandLine,
            Path,
        }


        private bool _inputValueReady;
        private bool _inplace;
        private LinkedList<string> _history = new LinkedList<string>();
        private LinkedListNode<string> _current = null;

        public MainPage()
        {
            InitializeComponent();

            Input.GotFocus += (sender, e) =>
            {
                Input.Foreground = new SolidColorBrush(Colors.Black);
                if (!_inputValueReady)
                {
                    Input.Text = "";
                    _inputValueReady = true;
                }
            };
            Input.LostFocus += (sender, e) =>
            {
                UpdateInputToHistory(Input.Text);

                if (string.IsNullOrWhiteSpace(Input.Text))
                {
                    Input.Foreground = new SolidColorBrush(Colors.LightGray);
                    Input.Text = "Enter text here ...";
                    _inputValueReady = false;
                }
            };

            UpdateUndoRedoUI();
            UpdateBtnEnabledStates();
        }

        private string InputText() => _inputValueReady ? Input.Text : "";

        private void SetInputText(string s, bool updateHistory = true)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                Input.Foreground = new SolidColorBrush(Colors.LightGray);
                Input.Text = "Enter text here ...";
                _inputValueReady = false;
            }
            else
            {
                Input.Foreground = new SolidColorBrush(Colors.Black);
                Input.Text = s;
                _inputValueReady = true;
            }

            if (updateHistory)
            {
                UpdateInputToHistory(s);
            }
        }

        private void UpdateInputToHistory(string s)
        {
            if (_current != null)
            {
                for (var p = _current.Next; p != null; )
                {
                    var next = p.Next;
                    _history.Remove(p);
                    p = next;
                }
            }
            _history.AddLast(s);
            _current = _history.Last;
            UpdateUndoRedoUI();
        }

        private void NormalizeOnClick(object sender, RoutedEventArgs e)
        {
            var s = NormalizePath(InputText());
            SetResult(s);
        }

        private void ToCodeOnClick(object sender, RoutedEventArgs e)
        {
            var s = NormalizePath(InputText());
            s = ToCode(s, false);
            SetResult(s);
        }

        private void ToCodeSlashOnClick(object sender, RoutedEventArgs e)
        {
            var s = NormalizePath(InputText());
            s = ToCode(s, true);
            SetResult(s);
        }

        private void PyArgsToCommandOnClick(object sender, RoutedEventArgs e)
        {
            var args = PyArgsToCommand(InputText());
            var sb = new StringBuilder();
            foreach (var arg in args)
            {
                sb.Append(arg);
                sb.Append(' ');
            }
            var res = sb.ToString().TrimEnd();
            SetResult(res);
        }

        private void CommandToPyArgsOnClick(object sender, RoutedEventArgs e)
        {
            var args = CommandToPyArgs(InputText());
            var sb = new StringBuilder("[");
            foreach (var arg in args)
            {
                sb.Append(arg);
                sb.Append(',');
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == ',')
            {
                sb.Remove(sb.Length - 1, 1);
            }
            sb.Append(']');
            SetResult(sb.ToString());
        }

        private void QuoteOnClick(object sender, RoutedEventArgs e)
        {
            var s = Escape(InputText(), '"');
            SetResult($"\"{s}\"");
        }

        private void QuoteSingleOnClick(object sender, RoutedEventArgs e)
        {
            var s = Escape(InputText(), '\'');
            SetResult($"\'{s}\'");
        }

        private void UnquoteOnClick(object sender, RoutedEventArgs e)
        {
            var s = InputText().Trim();
            if (s.Length > 2 && s[0] == s[s.Length - 1] && (s[0] == '\'' || s[0] == '"'))
            {
                var c = s[0];
                s = s.Substring(1, s.Length - 2);
                s = UnEscape(s);
            }
            SetResult(s);
        }

        private bool StartWithDriverLetter(string s)
        {
            if (s.Length < 2) return false;
            if (char.IsLetter(s[0]))
            {
                var i = 0;
                for (; i < s.Length && char.IsLetter(s[i]); i++) ;
                if (i >= s.Length) return false;
                return s[i] == ':';
            }
            return false;
        }

        private bool IsWindowsPath(string s)
            => s.StartsWith(@"\\\\") || s.Contains("\\") || StartWithDriverLetter(s);

        private string NormalizePath(string input)
        {
            var r = input.Trim();
            if (IsWindowsPath(r))
            {
                r = r.Replace(@"\\", @"\");
                r = r.Replace("/", @"\");
            }
            else
            {
                // TODO anything needed?
            }
            return r;
        }

        private string ToCode(string normalizedPath, bool useSlash)
        {
            if (IsWindowsPath(normalizedPath))
            {
                if (useSlash)
                {
                    if (normalizedPath.StartsWith(@"\\"))
                    {
                        var r = @"\\\\"
                            + normalizedPath.Substring(2).Replace(@"\", "/");
                        return r;
                    }
                    else
                    {
                        return normalizedPath.Replace(@"\", "/");
                    }
                }
                else
                {
                    return normalizedPath.Replace(@"\", @"\\");
                }
            }
            else
            {
                return normalizedPath.Replace(@"\", @"\\");
            }
        }

        private IEnumerable<string> SplitBy(string s, Predicate<char> isSplitter)
        {
            var inQuote = false;
            var last = 0;
            foreach (var (c, i, b) in JsonValueHelper.DeEscape(s, () => inQuote))
            {
                if ((c == '"' || c == '\'') && !b) // doesn't pair, assuming user takes care
                {
                    inQuote = !inQuote;
                    continue;
                }

                if (!inQuote && isSplitter(c))
                {
                    if (i > last)
                    {
                        yield return s.Substring(last, i - last);
                    }
                    last = i + 1;
                }
            }

            var t = s.Substring(last, s.Length - last);
            if (!string.IsNullOrWhiteSpace(t))
            {
                yield return t;
            }
        }

        private string TrimQuotes(string s)
        {
            if (s.Length < 2) return s;
            if (s[0] == '\'' && s[s.Length - 1] == '\'') return s.Trim('\'');
            if (s[0] == '"' && s[s.Length - 1] == '"') return s.Trim('"');
            return s;
        }

        // s has space trimed
        private bool NeedQuote(string s)
        {
            for (var i = 1; i < s.Length - 1; i++)
            {
                if (char.IsWhiteSpace(s[i]) || s[i] == '"' || s[i] == '\'')
                {
                    return true;
                }
            }
            return false;
        }

        private string Escape(string s, char qchar)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (c == qchar || c == '\\')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private string UnEscape(string s)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '\\')
                {
                    i++;
                    if (i >= s.Length)
                    {
                        break;
                    }
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private IEnumerable<string> PyArgsToCommand(string s)
        {
            var r = s.Trim();
            r = r.Trim('[', ']');
            var ss = SplitBy(r, c => c == ',');
            foreach (var seg in ss)
            {
                var arg = seg.Trim();
                if (!NeedQuote(arg))
                {
                    arg = TrimQuotes(arg);
                }
                arg = NormalizePath(arg);
                yield return arg;
            }
        }

        private IEnumerable<string> CommandToPyArgs(string s, char quoteChar = '\"')
        {
            var r = s.Trim();
            var ss = SplitBy(r, c => char.IsWhiteSpace(c));
            foreach (var seg in ss)
            {
                var arg = seg.Trim();
                arg = NormalizePath(arg);
                arg = Escape(arg, quoteChar);
                yield return $"{quoteChar}{arg}{quoteChar}";
            }
        }

        private bool IsQuoted(string s)
        {
            return (s.StartsWith('\'') && s.EndsWith('\'')
                || s.StartsWith('\"') && s.EndsWith('\"'));
        }

        private InputTypes DetectType(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return InputTypes.Empty;
            }
            var sc = SplitBy(s, c => c == ',');
            if (sc.Count() > 1 || s.StartsWith('[') && s.EndsWith(']'))
            {
                return InputTypes.PythonArgs;
            }
            var ss = SplitBy(s, c=>char.IsWhiteSpace(c));
            if (ss.Count() > 1)
            {
                return InputTypes.CommandLine;
            }
            return InputTypes.Path;
        }

        private void InputOnTextChanged(object sender, TextChangedEventArgs e)
            => UpdateBtnEnabledStates();

        private void UpdateBtnEnabledStates()
        {
            var s = InputText();
            var t = DetectType(s);
            UnquoteBtn.IsEnabled = IsQuoted(s);
            switch (t)
            {
                case InputTypes.Empty:
                    NormalizeBtn.IsEnabled = false;
                    ToCodeBtn.IsEnabled = false;
                    ToCodeSlashBtn.IsEnabled = false;
                    PyArgsToCommandBtn.IsEnabled = false;
                    CommandToPyArgsBtn.IsEnabled = false;
                    break;
                case InputTypes.PythonArgs:
                    NormalizeBtn.IsEnabled = false;
                    ToCodeBtn.IsEnabled = false;
                    ToCodeSlashBtn.IsEnabled = false;
                    PyArgsToCommandBtn.IsEnabled = true;
                    CommandToPyArgsBtn.IsEnabled = false;
                    break;
                case InputTypes.CommandLine:
                    NormalizeBtn.IsEnabled = false;
                    ToCodeBtn.IsEnabled = false;
                    ToCodeSlashBtn.IsEnabled = false;
                    PyArgsToCommandBtn.IsEnabled = false;
                    CommandToPyArgsBtn.IsEnabled = true;
                    break;
                case InputTypes.Path:
                    NormalizeBtn.IsEnabled = true;
                    ToCodeBtn.IsEnabled = true;
                    ToCodeSlashBtn.IsEnabled = true;
                    PyArgsToCommandBtn.IsEnabled = false;
                    CommandToPyArgsBtn.IsEnabled = true;
                    break;
            }
        }

        private void CopyToClipboardOnClick(object sender, RoutedEventArgs e)
        {
            var s = GetResult();
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(s);
            Clipboard.SetContent(pkg);
        }

        private void InplaceOnClicked(object sender, RoutedEventArgs e)
            => UpdateInplace();

        private void UpdateInplace()
        {
            _inplace = Inplace.IsChecked == true;
            Output.IsEnabled = !_inplace;
        }

        private void SetResult(string s)
        {
            if (_inplace)
            {
                SetInputText(s);
            }
            else
            {
                Output.Text = s;
            }
        }

        private string GetResult() => _inplace ? InputText() : Output.Text;

        private void UpdateUndoRedoUI()
        {
            if (_current == null)
            {
                UndoBtn.IsEnabled = false;
                RedoBtn.IsEnabled = false;
            }
            else
            {
                UndoBtn.IsEnabled = (_current != _history.First);
                RedoBtn.IsEnabled = (_current != _history.Last);
            }
        }

        private void UndoOnClick(object sender, RoutedEventArgs e)
        {
            if (_current != null && _current.Previous != null)
            {
                _current = _current.Previous;
                SetInputText(_current.Value, false);
            }
            UpdateUndoRedoUI();
        }

        private void RedoOnClick(object sender, RoutedEventArgs e)
        {
            if (_current != null && _current.Next != null)
            {
                _current = _current.Next;
                SetInputText(_current.Value, false);
            }
            UpdateUndoRedoUI();
        }
    }
}
