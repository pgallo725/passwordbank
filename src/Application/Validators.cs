using PasswordBank.DataModel;
using PasswordBank.Foundation;
using Spectre.Console;
using System.Buffers;
using System.Net.Mail;

namespace PasswordBank.Application
{
    /// <summary>
    /// A struct used to pass state and data to <see cref="IValidator{T}"/> static types.
    /// </summary>
    /// <param name="Database">A database reference, useful to query some data during validation.</param>
    /// <param name="PreviousIdentifier">The previous value of an identifier (used by <see cref="IdentifierValidator"/>).</param>
    internal readonly record struct ValidationContext
    (
        Database? Database = null,
        string? PreviousIdentifier = null
    );


    /// <summary>
    /// Generic interface to implement input validation.
    /// </summary>
    internal interface IValidator<T>
    {
        /// <summary>
        /// Validates an input value using a set of implementation-defined rules.
        /// </summary>
        /// <param name="value">The input value to validate.</param>
        /// <param name="context">Additional data required to validate the input value.</param>
        /// <returns>
        /// A <see cref="ValidationResult"/> with the outcome of the validation checks.
        /// </returns>
        public static abstract ValidationResult Validate(T value, ValidationContext context);
    }


    /// <summary>
    /// Implements no validation, always returns success.
    /// </summary>
    internal class NullValidator<T> : IValidator<T>
    {
        public static ValidationResult Validate(T value, ValidationContext context)
        {
            return ValidationResult.Success();
        }
    }


    /// <summary>
    /// Implements basic validation, only checking the input length against the maximum value.
    /// </summary>
    internal class BasicValidator : IValidator<string>, IValidator<SecretString>
    {
        public static ValidationResult Validate(string value, ValidationContext context)
        {
            if (value.Length > 128)
                return ValidationResult.Error("The input length is limited to a maximum of 128 characters.");
            return ValidationResult.Success();
        }

        public static ValidationResult Validate(SecretString value, ValidationContext context)
        {
            if (value.GetLength() > 128)
                return ValidationResult.Error("The input length is limited to a maximum of 128 characters.");
            return ValidationResult.Success();
        }
    }


    /// <summary>
    /// Implements input validation of a string that should represent an email address.
    /// </summary>
    internal class EmailAddressValidator : IValidator<string>
    {
        public static ValidationResult Validate(string emailAddress, ValidationContext context)
        {
            // Consider an email address correct if it can be used to construct a valid MailAddress object
            if (emailAddress.Length > 128)
                return ValidationResult.Error("The input length is limited to a maximum of 128 characters.");
            else if (MailAddress.TryCreate(emailAddress, out var _))
                return ValidationResult.Success();
            else return ValidationResult.Error("The input does not match the format of a valid email address.");
        }
    }


    /// <summary>
    /// Implements input validation of a string that will be used as a credential identifier.
    /// </summary>
    internal class IdentifierValidator : IValidator<string>
    {
        public static ValidationResult Validate(string identifier, ValidationContext context)
        {
            if (context.Database is null)
                throw new ArgumentNullException(nameof(context), "A database is required to validate an identifier.");

            if (string.IsNullOrWhiteSpace(identifier))
                return ValidationResult.Error("An identifier must contain atleast 1 non-whitespace character.");
            else if (identifier.Length > 128)
                return ValidationResult.Error("The input length is limited to a maximum of 128 characters.");
            else if (string.Equals(identifier, context.PreviousIdentifier, StringComparison.Ordinal))
                return ValidationResult.Success();
            else if (context.Database.Contains(identifier))
                return ValidationResult.Error("A credential with the same identifier already exists in the database.");
            else return ValidationResult.Success();
        }
    }


    /// <summary>
    /// Implements input validation of a SecretString that represents an account's password.
    /// </summary>
    internal class PasswordValidator : IValidator<SecretString>
    {
        public static ValidationResult Validate(SecretString password, ValidationContext context)
        {
            // Check that the password is not empty/whitespace and contains atleast 3 characters
            if (SecretString.IsNullOrWhiteSpace(password))
                return ValidationResult.Error("A password must not be empty or made only of whitespace characters.");
            else if (password.GetLength() > 128)
                return ValidationResult.Error("The input length is limited to a maximum of 128 characters.");
            else if (password.GetLength() < 3)
                return ValidationResult.Error("A password must contain at least 3 characters.");
            else return ValidationResult.Success();
        }
    }


    /// <summary>
    /// Implements input validation of a SecretString that will be used as master password.<br/>
    /// In addition to basic validity checks, this also makes sure that the master password satisfies
    ///  a set of minimum strength requirements.
    /// </summary>
    internal class MasterPasswordValidator : IValidator<SecretString>
    {
        public static ValidationResult Validate(SecretString masterPassword, ValidationContext context)
        {
            /* Check that the master password satisfies the following password strength requirements:
             *  - At least 12 characters long
             *  - At least 1 lowercase letter
             *  - At least 1 uppercase letter
             *  - At least 1 digit character
             *  - At least 1 symbol character
             */
            if (SecretString.IsNullOrWhiteSpace(masterPassword))
                return ValidationResult.Error("A password must not be empty or made only of whitespace characters.");
            else if (masterPassword.GetLength() > 128)
                return ValidationResult.Error("The input length is limited to a maximum of 128 characters.");
            else if (masterPassword.GetLength() < 12)
                return ValidationResult.Error("The master password must be at least 12 characters long.");
            else
            {
                bool hasLowercase = false;
                bool hasUppercase = false;
                bool hasDigit = false;
                bool hasSymbol = false;

                ReadOnlySpan<char> masterPasswordChars = masterPassword.GetData();
                for (int i = 0; i < masterPassword.GetLength(); i++)
                {
                    char ch = masterPasswordChars[i];
                    if (!hasLowercase && char.IsLower(ch))
                        hasLowercase = true;
                    if (!hasUppercase && char.IsUpper(ch))
                        hasUppercase = true;
                    if (!hasDigit && char.IsDigit(ch))
                        hasDigit = true;
                    if (!hasSymbol && (char.IsSymbol(ch) || char.IsPunctuation(ch)))
                        hasSymbol = true;
                }
                if (!hasLowercase)
                    return ValidationResult.Error("The master password must contain at least 1 lowercase letter.");
                else if (!hasUppercase)
                    return ValidationResult.Error("The master password must contain at least 1 uppercase letter.");
                else if (!hasDigit)
                    return ValidationResult.Error("The master password must contain at least 1 digit character.");
                else if (!hasSymbol)
                    return ValidationResult.Error("The master password must contain at least 1 symbol character.");
                else return ValidationResult.Success();
            }
        }
    }
}
