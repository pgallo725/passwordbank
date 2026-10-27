using PasswordBank.DataModel;
using Spectre.Console;
using System.Buffers;
using System.Net.Mail;

namespace PasswordBank.Application
{
    /// <summary>
    /// Generic interface of an object that implements text input validation.
    /// </summary>
    internal interface IValidator
    {
        public ValidationResult Validate(string value);
        public ValidationResult Validate(SecretString value);
    }


    /// <summary>
    /// Implements no validation, always returns success.
    /// </summary>
    internal class NullValidator : IValidator
    {
        public ValidationResult Validate(string value)
        {
            return ValidationResult.Success();
        }

        public ValidationResult Validate(SecretString value)
        {
            return ValidationResult.Success();
        }
    }


    /// <summary>
    /// Implements basic validation, only checking the input length against the maximum value.
    /// </summary>
    internal class BasicValidator : IValidator
    {
        public ValidationResult Validate(string value)
        {
            if (value.Length > 256)
                return ValidationResult.Error("The input length is limited to a maximum of 256 characters.");
            return ValidationResult.Success();
        }

        public ValidationResult Validate(SecretString value)
        {
            if (value.GetLength() > 256)
                return ValidationResult.Error("The input length is limited to a maximum of 256 characters.");
            return ValidationResult.Success();
        }
    }


    /// <summary>
    /// Implements validation of a string input that should represent an email address.
    /// </summary>
    internal class EmailAddressValidator : IValidator
    {
        public ValidationResult Validate(string emailAddress)
        {
            // Consider an email address correct if it can be used to construct a valid MailAddress object
            if (emailAddress.Length > 256)
                return ValidationResult.Error("The input length is limited to a maximum of 256 characters.");
            else if (MailAddress.TryCreate(emailAddress, out var _))
                return ValidationResult.Success();
            else return ValidationResult.Error("The input does not match the format of a valid email address.");
        }

        public ValidationResult Validate(SecretString _)
        {
            // EmailAddressValidator should never be used with SecretString objects
            throw new NotSupportedException();
        }
    }


    /// <summary>
    /// Implements validation of a string input that will be used as the name of an account.
    /// </summary>
    /// <param name="database">A reference to the credentials database.</param>
    internal class AccountNameValidator(Database? database) : IValidator
    {
        public virtual ValidationResult Validate(string accountName)
        {
            // Check that the account name is not empty and doesn't already exist in the database
            if (string.IsNullOrWhiteSpace(accountName))
                return ValidationResult.Error("The account name must contain atleast 1 non-whitespace character.");
            else if (accountName.Length > 256)
                return ValidationResult.Error("The input length is limited to a maximum of 256 characters.");
            else if (database is null)
                return ValidationResult.Error("Cannot access the database for checking the username.");
            else if (database.Contains(accountName))
                return ValidationResult.Error("An account with the same name already exists in the database.");
            else return ValidationResult.Success();
        }

        public virtual ValidationResult Validate(SecretString _)
        {
            // AccountNameValidator should never be used with SecretString objects
            throw new NotSupportedException();
        }
    }


    /// <summary>
    /// Implements validation of an edited string that will be used as the name of an account.<br/>
    /// This extends <see cref="AccountNameValidator"/> by allowing an unchanged account name,
    /// which would otherwise be rejected because it already exists in the database.
    /// </summary>
    /// <param name="database">A reference to the credentials database.</param>
    /// <param name="prevAccountName">The previous name assigned to the account that is being modified.</param>
    internal class AccountNameEditValidator(Database? database, string prevAccountName)
        : AccountNameValidator(database)
    {
        public override ValidationResult Validate(string accountName)
        {
            if (string.Compare(accountName, prevAccountName, StringComparison.Ordinal) == 0)
                return ValidationResult.Success();
            else return base.Validate(accountName);
        }

        public override ValidationResult Validate(SecretString _)
        {
            // AccountNameEditValidator should never be used with SecretString objects
            throw new NotSupportedException();
        }
    }


    /// <summary>
    /// Implements validation of a string input that represents an account's password.
    /// </summary>
    internal class PasswordValidator : IValidator
    {
        public ValidationResult Validate(string _)
        {
            // PasswordValidator should NEVER be used with plain .NET strings
            throw new NotSupportedException();
        }

        public ValidationResult Validate(SecretString password)
        {
            // Check that the password is not empty/whitespace and contains atleast 3 characters
            if (SecretString.IsNullOrWhiteSpace(password))
                return ValidationResult.Error("A password must not be empty or made only of whitespace characters.");
            else if (password.GetLength() > 256)
                return ValidationResult.Error("The input length is limited to a maximum of 256 characters.");
            else if (password.GetLength() < 3)
                return ValidationResult.Error("A password must contain at least 3 characters.");
            else return ValidationResult.Success();
        }
    }


    /// <summary>
    /// Implements validation of a string input that will be used as the database's master password.<br/>
    /// In addition to basic validity checks, this also makes sure that the master password satisfies
    ///  a set of minimum strength requirements.
    /// </summary>
    internal class MasterPasswordValidator : IValidator
    {
        private static readonly SearchValues<char> digits = SearchValues.Create
        (
            ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9']
        );
        private static readonly SearchValues<char> lowercase = SearchValues.Create
        (
            ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
             'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z']
        );
        private static readonly SearchValues<char> uppercase = SearchValues.Create
        (
            ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
             'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z']
        );

        public ValidationResult Validate(string _)
        {
            // MasterPasswordValidator should NEVER be used with plain .NET strings
            throw new NotSupportedException();
        }

        public ValidationResult Validate(SecretString masterPassword)
        {
            /* Check that the master password satisfies the following password strength requirements:
             *  - At least 8 characters long
             *  - At least 1 lowercase letter
             *  - At least 1 uppercase letter
             *  - At least 1 digit character
             */
            if (SecretString.IsNullOrWhiteSpace(masterPassword))
                return ValidationResult.Error("A password must not be empty or made only of whitespace characters.");
            else if (masterPassword.GetLength() > 256)
                return ValidationResult.Error("The input length is limited to a maximum of 256 characters.");
            else if (masterPassword.GetLength() < 8)
                return ValidationResult.Error("The master password must be at least 8 characters long.");
            else if (!masterPassword.ContainsAny(lowercase))
                return ValidationResult.Error("The master password must contain at least 1 lowercase letter.");
            else if (!masterPassword.ContainsAny(uppercase))
                return ValidationResult.Error("The master password must contain at least 1 uppercase letter.");
            else if (!masterPassword.ContainsAny(digits))
                return ValidationResult.Error("The master password must contain at least 1 digit character.");
            else return ValidationResult.Success();
        }
    }
}
