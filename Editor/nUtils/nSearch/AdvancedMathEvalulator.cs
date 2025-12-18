using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

public static class AdvancedMathEvaluator
{
    private static readonly Dictionary<string, double> Constants = new Dictionary<string, double>
    {
        { "pi", Math.PI },
        { "e", Math.E },
        { "tau", Math.PI * 2 }
    };

    private static readonly Dictionary<string, Func<double, double>> Functions = new Dictionary<string, Func<double, double>>
    {
        { "sqrt", Math.Sqrt },
        { "abs", Math.Abs },
        { "sin", Math.Sin },
        { "cos", Math.Cos },
        { "tan", Math.Tan },
        { "asin", Math.Asin },
        { "acos", Math.Acos },
        { "atan", Math.Atan },
        { "sinh", Math.Sinh },
        { "cosh", Math.Cosh },
        { "tanh", Math.Tanh },
        { "ln", Math.Log },
        { "log", Math.Log10 },
        { "exp", Math.Exp },
        { "floor", Math.Floor },
        { "ceil", Math.Ceiling },
        { "round", x => Math.Round(x) }
    };

    public static string EvaluateExpression(string expression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
                return null;

            // Preprocess the expression
            expression = expression.ToLower().Replace(" ", "");

            // Replace constants
            foreach (var constant in Constants)
            {
                expression = Regex.Replace(expression, @"\b" + constant.Key + @"\b", constant.Value.ToString(CultureInfo.InvariantCulture));
            }

            // Handle implicit multiplication (e.g., 2pi -> 2*pi, 5(3+2) -> 5*(3+2))
            expression = Regex.Replace(expression, @"(\d)([a-z(])", "$1*$2");
            expression = Regex.Replace(expression, @"(\))(\d)", "$1*$2");
            expression = Regex.Replace(expression, @"(\))(\()", "$1*$2");

            double result = Evaluate(expression);

            // Format result nicely
            if (double.IsInfinity(result) || double.IsNaN(result))
                return null;

            // Round to reasonable precision
            if (Math.Abs(result) < 1e-10)
                result = 0;

            return result.ToString("G15");
        }
        catch
        {
            return null;
        }
    }

    private static double Evaluate(string expression)
    {
        return ParseExpression(ref expression);
    }

    private static double ParseExpression(ref string expr)
    {
        double result = ParseTerm(ref expr);

        while (expr.Length > 0 && (expr[0] == '+' || expr[0] == '-'))
        {
            char op = expr[0];
            expr = expr.Substring(1);
            double term = ParseTerm(ref expr);
            result = op == '+' ? result + term : result - term;
        }

        return result;
    }

    private static double ParseTerm(ref string expr)
    {
        double result = ParseFactor(ref expr);

        while (expr.Length > 0 && (expr[0] == '*' || expr[0] == '/' || expr[0] == '%'))
        {
            char op = expr[0];
            expr = expr.Substring(1);
            double factor = ParseFactor(ref expr);

            if (op == '*')
                result *= factor;
            else if (op == '/')
                result /= factor;
            else
                result %= factor;
        }

        return result;
    }

    private static double ParseFactor(ref string expr)
    {
        double result = ParsePower(ref expr);
        return result;
    }

    private static double ParsePower(ref string expr)
    {
        double result = ParseUnary(ref expr);

        if (expr.Length > 0 && expr[0] == '^')
        {
            expr = expr.Substring(1);
            double exponent = ParsePower(ref expr); // Right associative
            result = Math.Pow(result, exponent);
        }

        return result;
    }

    private static double ParseUnary(ref string expr)
    {
        if (expr.Length > 0 && (expr[0] == '+' || expr[0] == '-'))
        {
            char op = expr[0];
            expr = expr.Substring(1);
            return op == '-' ? -ParseUnary(ref expr) : ParseUnary(ref expr);
        }

        return ParsePrimary(ref expr);
    }

    private static double ParsePrimary(ref string expr)
    {
        // Check for function calls
        foreach (var func in Functions)
        {
            if (expr.StartsWith(func.Key + "("))
            {
                expr = expr.Substring(func.Key.Length + 1); // Remove "function("
                double arg = ParseExpression(ref expr);

                if (expr.Length > 0 && expr[0] == ')')
                    expr = expr.Substring(1);

                return func.Value(arg);
            }
        }

        // Check for parentheses
        if (expr.Length > 0 && expr[0] == '(')
        {
            expr = expr.Substring(1);
            double result = ParseExpression(ref expr);

            if (expr.Length > 0 && expr[0] == ')')
                expr = expr.Substring(1);

            return result;
        }

        // Parse number
        return ParseNumber(ref expr);
    }

    private static double ParseNumber(ref string expr)
    {
        int i = 0;
        int dotCount = 0;

        // Handle negative sign already processed in ParseUnary
        while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.'))
        {
            if (expr[i] == '.')
            {
                dotCount++;
                if (dotCount > 1) break;
            }
            i++;
        }

        // Handle scientific notation
        if (i < expr.Length && (expr[i] == 'e') && i > 0)
        {
            i++;
            if (i < expr.Length && (expr[i] == '+' || expr[i] == '-'))
                i++;

            while (i < expr.Length && char.IsDigit(expr[i]))
                i++;
        }

        if (i == 0)
            throw new Exception("Expected number");

        string numStr = expr.Substring(0, i);
        expr = expr.Substring(i);

        return double.Parse(numStr, CultureInfo.InvariantCulture);
    }
}