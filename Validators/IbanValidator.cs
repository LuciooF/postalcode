﻿using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Validators.Indexers;
using Validators.Interfaces;
using Validators.Models;


namespace Validators
{

    /// <summary>
    ///     used as all business logic behind iban of European countries.
    ///     validates and formats the iban according to the iban country.
    /// </summary>
    public class IbanValidator
        : IIbanValidator
    {

        /// <summary>
        ///     used as internal business rules of European iban.
        /// </summary>
        private readonly IIbanModel _model;


        /// <summary>
        ///      used as internal business rules of iban of selected country.
        /// </summary>
        private IbanRuleSetModel _logic;


        /// <summary>
        ///     used as internal business logic for sanity check, based on the found index.
        /// </summary>
        private const string _alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";


        /// <summary>
        ///     used as constructor to initiliaze the class with the internal business rules of all internal iban.
        /// </summary>
        public IbanValidator() => _model = new IbanModel();


        /// <summary>
        ///     used as an iban example of a country.
        /// </summary>
        public string Example { get => _logic.Example; }


        /// <summary>
        ///     used as error message in case of <seealso cref="IsValid"/> = false;
        /// </summary>
        public string ErrorMessage { get; private set; }


        /// <summary>
        ///     used as the country of the iban value. 
        /// </summary>
        public Countries Country => _logic.Country;


        /// <summary>
        ///     used to validate given iban.
        ///     when false an <seealso cref="ErrorMessage"/> will be given.
        /// </summary>
        public bool IsValid
        {
            get; private set;
        }


        /// <summary>
        ///     used as to validate an iban value
        /// </summary>
        /// <param name="value">
        ///     used as the iban value to be validated.
        /// </param>
        /// <param name="result">
        ///     used as out value of the formatted iban value <see cref="IsValid"/> = true
        ///     else the provided value is used as result.
        /// </param>
        /// <returns>
        ///     <see cref="IsValid"/> is a valid iban or not?
        /// </returns>
        public bool Validate(string value, out string result)
        {
            result = value.Trim()
                ?? throw new ArgumentException(nameof(value));

            // => use TwoLetterISORegionName from iban value as key
            if (!_model.Rules.TryGetValue(value.Substring(0, 2).ToUpperInvariant(), out _logic))
                throw new ArgumentException("no matching country found.");

            var match = Regex.Match(result, _logic.RegexPattern);

            IsValid = match.Success;

            if (!IsValid)
            {
                SetErrorMessage(result);

                return IsValid;
            }
 
            IsValid = IsSanityValid(match);

            // todo: format match value and as ternary expression. 
            if (IsValid)
            {
                // to do formatting.
            }
            else
            { 
                ErrorMessage = $"Ïban value of \"{result}\" is not valid as sanity validation. Use as example \"{_logic.Example}\" for country {Country.ToString()}.";
            }

            return IsValid;
        }


        private void SetErrorMessage(string value)
        {
            // todo: use and add formatter to replace everything
            int length = value.Replace(" ", string.Empty).Length;

            ErrorMessage = length == _logic.Length
                ? $"Iban value of \"{value}\" is not valid. Use as example \"{_logic.Example}\" for country {Country.ToString()}."
                : $"Length {length} of given Iban is not valid. It should be an Length of {_logic.Length} for country {Country.ToString()}. Use as example \"{Example}\".";
        }


        /// <summary>
        ///     used to validate as sanity check, based on modules 97
        /// </summary>
        /// <param name="match">
        ///     used as the match of the regular expression to generate the sanity check.
        /// </param>
        /// <returns>
        ///     <see cref="bool"/> depending if sanity is valid or not.
        /// </returns>
        private bool IsSanityValid(Match match)
        {
            double checkSum = 0;
            GenericIndexer<byte> sanityIndexer = CreateSanityIndexer(match);

            // research: System.ReadOnlySpan<char> => net core 3.0 new implementation?
            foreach (byte value in sanityIndexer)
            {
                // => sanity check, current value * 10 + value modulus of 97.
                checkSum *= 10;
                checkSum += value;
                checkSum %= 97;
            }

            return checkSum == 1;
        }


        /// <summary>
        ///     used as the complete sanity check <see cref="GenericIndexer{T}"/> for given regular expression match.
        /// </summary>
        /// <param name="match">
        ///     used as regular expression match for the sanity check.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     throws this exception, when it can not convert <paramref name="match"/> as an <see cref="int"/>.
        /// </exception>
        /// <returns>
        ///     <see cref="GenericIndexer{byte}"/> to be used for the sanity check.
        /// </returns>
        private GenericIndexer<byte> CreateSanityIndexer(Match match)
        {
            // => always get sanity number from country
            var country = CharAsInt(match.Groups.Single(group => group.Name == "country").Value.ToCharArray());

            // => always get sanity number from bank name, in case regular expression has bank name?
            int name = 0;
            if (match.Groups.Any(item => item.Name.Equals("name", StringComparison.OrdinalIgnoreCase)))
                name = CharAsInt(match.Groups.Single(group => group.Name == "name").Value.ToCharArray());
            
            // => internal logic how we have to format the sanity check
            var formatAsNumbers = _logic.SanityFormat;
            
            // => formats sanity, based on regular expression group.Names and their values.
            foreach (Group group in match.Groups)
            {
                if (group.Name.Equals("country", StringComparison.OrdinalIgnoreCase))
                    formatAsNumbers = formatAsNumbers.Replace(string.Format("<{0}>", group.Name), country.ToString());

                if (group.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
                    formatAsNumbers = formatAsNumbers.Replace(string.Format("<{0}>", group.Name), name.ToString());

                if (!string.IsNullOrWhiteSpace(group.Value))
                    formatAsNumbers = formatAsNumbers.Replace(string.Format("<{0}>", group.Name), group.Value);
            }

            // => validate sanity is a number
            if (!double.TryParse(formatAsNumbers, out double sanityNumber))
                throw new ArgumentOutOfRangeException(nameof(sanityNumber));

            // => reindex every didget [0- 9] as a byte
            return new GenericIndexer<byte>(
                formatAsNumbers.ToCharArray()
                    .Select(value => { return byte.Parse(value.ToString()); })
                );
        }


        // todo: convert as extension

        /// <summary>
        ///     used as the values that has to be converted to int for the sanity check.
        /// </summary>
        /// <param name="value">
        ///     used as the <see cref="char[]"/> to be converted as <see cref="int"/> value used for the sanity check.
        /// </param>
        /// <returns>
        ///     <see cref="int"/> value used for the sanity check.
        /// </returns>
        private int CharAsInt(char[] value)
        {
            var stringBuilder = new StringBuilder(value.Length * 2);

            foreach (var alphaNumber in value)
                stringBuilder.Append(CharAsInt(alphaNumber));

            if (!int.TryParse(stringBuilder.ToString(), out int result))
                throw new ArgumentException(nameof(value));

            return result;
        }


        // todo: convert as extension
        
        /// <summary>
        ///     used as to convert a letter to an int.
        /// </summary>
        /// <param name="value">
        ///     used as the char, that has to be converted.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     throws exception <paramref name="value"/> has not been found.
        /// </exception>
        /// <returns>
        ///     <see cref="int"/> value of <paramref name="value"/> to be used for the sanity check.
        /// </returns>
        private int CharAsInt(char value)
        {
            var index = _alphabet.IndexOf(value);

            // => value not found!
            if (index == -1)
                throw new ArgumentOutOfRangeException(nameof(value));

            // => where 0 = 0, 1 = 1, ..., 9 = 9, A = 10, B = 11, ..., Z = 35
            return index;
        }
    }
}