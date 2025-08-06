using System.ComponentModel.DataAnnotations;

namespace FEENALOoFINALE.Attributes
{
    public class SafeStringAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is string str)
            {
                // Check for potentially dangerous content
                var dangerousPatterns = new[]
                {
                    "<script", "javascript:", "vbscript:", "onload=", "onerror=",
                    "eval(", "setTimeout(", "setInterval(", "document.cookie",
                    "window.location", "alert(", "confirm(", "prompt("
                };

                return !dangerousPatterns.Any(pattern => 
                    str.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            }
            return true;
        }

        public override string FormatErrorMessage(string name)
        {
            return $"The {name} field contains potentially unsafe content. Please remove any script tags or JavaScript code.";
        }
    }
}