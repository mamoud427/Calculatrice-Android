using System.Globalization;

namespace CALCULATRICE;

public partial class MainPage : ContentPage
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const int MaxDigits = 15;

    string _current = "0";      // nombre en cours de saisie (point décimal interne)
    string _expression = "";    // opération affichée au-dessus du résultat
    decimal? _left;             // opérande de gauche
    string? _op;                // opérateur en attente
    bool _startNew;             // le prochain chiffre remplace l'affichage
    string? _error;             // message d'erreur éventuel
    double _resultBaseSize = 48;

    public MainPage()
    {
        InitializeComponent();
        // Alignement à droite de l'opération même dans un ScrollView horizontal
        ExpressionScroll.SizeChanged += (_, _) =>
            ExpressionLabel.MinimumWidthRequest = ExpressionScroll.Width;
        UpdateDisplay();
    }

    // Adaptation à l'orientation et à la taille de l'écran
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        TopBar.IsVisible = height >= width;   // on gagne de la place en paysage

        double btnFont = Math.Clamp(Math.Min(height / 7, width / 4) * 0.4, 14, 34);
        foreach (var b in Keypad.Children.OfType<Button>())
            b.FontSize = btnFont;

        _resultBaseSize = Math.Clamp(height / 12, 22, 56);
        UpdateDisplay();
    }

    // ---------- Saisie ----------
    void OnDigit(object? sender, EventArgs e)
    {
        if (_error != null) Reset();
        if (_op == null) _expression = "";
        string d = ((Button)sender!).Text;

        if (_startNew || _current == "0") { _current = d; _startNew = false; }
        else if (_current.Count(char.IsDigit) < MaxDigits) _current += d;
        UpdateDisplay();
    }

    void OnDecimal(object? sender, EventArgs e)
    {
        if (_error != null) Reset();
        if (_op == null) _expression = "";

        if (_startNew) { _current = "0."; _startNew = false; }
        else if (!_current.Contains('.')) _current += ".";
        UpdateDisplay();
    }

    void OnOperator(object? sender, EventArgs e)
    {
        if (_error != null) return;
        string newOp = ((Button)sender!).Text;

        if (_op != null && !_startNew)
        {
            if (!Compute()) return;          // enchaînement : 2 + 3 × ...
        }
        else if (_op == null)
        {
            _left = Parse(_current);
        }

        _op = newOp;
        _startNew = true;
        _expression = $"{Format(_left!.Value)} {_op}";
        UpdateDisplay();
    }

    void OnEquals(object? sender, EventArgs e)
    {
        if (_error != null || _op == null) return;
        decimal right = Parse(_current);
        _expression = $"{Format(_left!.Value)} {_op} {Format(right)} =";

        if (!Compute()) return;
        _op = null; _left = null; _startNew = true;
        UpdateDisplay();
    }

    // ---------- Fonctions ----------
    void OnClear(object? sender, EventArgs e) { Reset(); UpdateDisplay(); }

    void OnBackspace(object? sender, EventArgs e)
    {
        if (_error != null) { Reset(); UpdateDisplay(); return; }
        if (_startNew) return;
        _current = _current[..^1];
        if (_current is "" or "-" or "-0") _current = "0";
        UpdateDisplay();
    }

    void OnSign(object? sender, EventArgs e)
    {
        if (_error != null || _current == "0") return;
        _current = _current.StartsWith('-') ? _current[1..] : "-" + _current;
        if (_op != null) _startNew = false;
        UpdateDisplay();
    }

    void OnPercent(object? sender, EventArgs e)
    {
        if (_error != null) return;
        decimal v = Parse(_current);
        // 200 + 10 %  ->  10 % de 200 = 20 ; sinon simple division par 100
        v = (_op != null && _left.HasValue) ? _left.Value * v / 100m : v / 100m;
        _current = Format(v);
        _startNew = _op == null;
        UpdateDisplay();
    }

    // ---------- Calcul ----------
    bool Compute()
    {
        decimal right = Parse(_current);
        try
        {
            switch (_op)
            {
                case "+": _left += right; break;
                case "−": _left -= right; break;
                case "×": _left *= right; break;
                case "÷":
                    if (right == 0) { ShowError("Division par zéro impossible"); return false; }
                    _left /= right;
                    break;
            }
        }
        catch (OverflowException)
        {
            ShowError("Résultat trop grand");
            return false;
        }
        _current = Format(_left!.Value);
        return true;
    }

    void ShowError(string message)
    {
        _error = message;
        _op = null; _left = null; _startNew = true;
        UpdateDisplay();
    }

    void Reset()
    {
        _current = "0"; _expression = "";
        _left = null; _op = null;
        _startNew = false; _error = null;
    }

    // ---------- Utilitaires ----------
    static decimal Parse(string s) => decimal.Parse(s, NumberStyles.Float, Inv);

    static string Format(decimal v)
    {
        v = decimal.Round(v, 10);
        return v == 0 ? "0" : v.ToString("0.##########", Inv);
    }

    void UpdateDisplay()
    {
        string text = _error ?? _current.Replace('.', ',');
        ExpressionLabel.Text = _expression.Replace('.', ',');
        ResultLabel.Text = text;

        double size = _resultBaseSize;
        if (_error != null) size = _resultBaseSize * 0.4;
        else if (text.Length > 9) size = Math.Max(_resultBaseSize * 9 / text.Length, 18);
        ResultLabel.FontSize = size;

        _ = ExpressionScroll.ScrollToAsync(ExpressionScroll.ContentSize.Width, 0, false);
    }
}