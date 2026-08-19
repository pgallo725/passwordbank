using PasswordBank.DataModel;
using System.Net.Mail;

namespace PasswordBank.Application
{
    /// <summary>
    /// Generic interface of an object that implements text input validation.
    /// </summary>
    internal interface IValidator
    {
        public (bool, string?) Validate(string value);
    }


    /// <summary>
    /// Implements no validation, always returns success.
    /// </summary>
    internal class NullValidator : IValidator
    {
        public (bool, string?) Validate(string value)
        {
            return (true, string.Empty);
        }
    }


    /// <summary>
    /// Implements validation of a string input that should represent an email address.
    /// </summary>
    internal class EmailAddressValidator : IValidator
    {
        public (bool, string?) Validate(string emailAddress)
        {
            // Consider an email address correct if it can be used to construct a valid MailAddress object
            if (MailAddress.TryCreate(emailAddress, out var _))
                return (true, string.Empty);
            else return (false, "The input does not match the format of a valid email address.");
        }
    }


    /// <summary>
    /// Implements validation of a string input that will be used as the name of an account.
    /// </summary>
    /// <param name="database">A reference to the credentials database.</param>
    internal class AccountNameValidator(Database? database) : IValidator
    {
        public virtual (bool, string?) Validate(string accountName)
        {
            // Check if the account name is not empty and doesn't already exist in the database
            if (string.IsNullOrWhiteSpace(accountName))
                return (false, "The account name must contain atleast 1 non-whitespace character.");
            if (database is null)
                return (false, "Cannot access the database for checking the username.");
            else if (database.Contains(accountName))
                return (false, "An account with the same name already exists in the database.");
            else return (true, string.Empty);
        }
    }


    /// <summary>
    /// Implements validation of an edited string that will be used as the name of an account.<br/>
    /// This extends <see cref="AccountNameValidator"/> by allowing an unchanged account name,
    /// which would otherwise be rejected because it already exists in the database.
    /// </summary>
    /// <param name="database">A reference to the credentials database.</param>
    /// <param name="currentInfo">The original credential record that is being modified.</param>
    internal class AccountNameEditValidator(Database? database, CredentialInfo currentInfo)
        : AccountNameValidator(database)
    {
        public override (bool, string?) Validate(string accountName)
        {
            if (string.Compare(accountName, currentInfo.Name, StringComparison.OrdinalIgnoreCase) == 0)
                return (true, string.Empty);
            else return base.Validate(accountName);
        }
    }
}
