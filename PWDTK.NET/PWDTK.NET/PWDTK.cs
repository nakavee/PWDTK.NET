﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Linq;

namespace PWDTK_DOTNET451
{
    /// <summary>
    /// Password Toolkit created by Ian Harris
    /// This library facilitates crypto random salt generating and password hashing using HMACSHA512 based PBKDF2
    /// Also it provides hash comparisson and password policy enforcement by regex (optional)
    /// Conatct: harro84@yahoo.com.au
    /// v2.0.0.1
    /// </summary>
    public static class PWDTK
    {
        #region PWDTK Structs and Constants

        /// <summary>
        /// The default character length to create salt strings
        /// </summary>
        public const Int32 CDefaultSaltLength = 64;

        /// <summary>
        /// The default iteration count for key stretching
        /// </summary>
        public const Int32 CDefaultIterationCount = 5000;

        /// <summary>
        /// The minimum size in characters the password hashing function will allow for a salt string, salt must be always greater than 8 for PBKDF2 key derivitation to function
        /// </summary>
        public const Int32 CMinSaltLength = 8;

        /// <summary>
        /// The maximum size a password can be to avoid massive passwords that force the initial hash to take so long it creates a DOS effect. Be careful increasing this, also make sure you catch exception for password too long!
        /// </summary>
        public const Int32 CMaxPasswordLength = 1024;

        /// <summary>
        /// The key length used in the PBKDF2 derive bytes and matches the output of the underlying HMACSHA512 psuedo random function
        /// </summary>
        public const Int32 CKeyLength = 64;

        /// <summary>
        /// A default password policy provided for use if you are unsure what to make your own PasswordPolicy
        /// </summary>
        public static PasswordPolicy CDefaultPasswordPolicy = new PasswordPolicy(1, 1, 2, 6, Int32.MaxValue);

        /// <summary>
        /// Below are regular expressions used for password to policy comparrisons
        /// </summary>
        private const String CNumbersRegex ="[\\d]";
        private const String CUppercaseRegex = "[A-Z]";
        private const String CNonAlphaNumericRegex = "[^0-9a-zA-Z]";

        /// <summary>
        /// A PasswordPolicy defines min and max password length and also minimum amount of Uppercase, Non-Alpanum and Numerics to be present in the password string
        /// </summary>
        public struct PasswordPolicy
        {
            private readonly int _aForceXUpperCase;            
            private readonly int _aForceXNonAlphaNumeric;
            private readonly int _aForceXNumeric;
            private readonly int _aPasswordMinLength;
            private readonly int _aPasswordMaxLength;

            /// <summary>
            /// Creates a new PasswordPolicy Struct
            /// </summary>
            /// <param name="xUpper">Forces at least this number of Uppercase characters</param>
            /// <param name="xNonAlphaNumeric">Forces at least this number of Special characters</param>
            /// <param name="xNumeric">Forces at least this number of Numeric characters</param>
            /// <param name="minLength">Forces at least this number of characters</param>
            /// <param name="maxLength">Forces at most this number of characters</param>
            public PasswordPolicy(int xUpper, int xNonAlphaNumeric, int xNumeric, int minLength, int maxLength)
            {
                _aForceXUpperCase = xUpper;
                _aForceXNonAlphaNumeric = xNonAlphaNumeric;
                _aForceXNumeric = xNumeric;
                _aPasswordMinLength = minLength;
                _aPasswordMaxLength = maxLength;
            }

            public int ForceXUpperCase
            {
                get
                {
                    return _aForceXUpperCase;
                }
            }

            public int ForceXNonAlphaNumeric
            {
                get
                {
                    return _aForceXNonAlphaNumeric;
                }
            }

            public int ForceXNumeric
            {
                get
                {
                    return _aForceXNumeric;
                }
            }

            public int PasswordMinLength
            {
                get
                {
                    return _aPasswordMinLength;
                }
            }

            public int PasswordMaxLength
            {
                get
                {
                    return _aPasswordMaxLength;
                }
            }
        }

        #endregion

        #region PWDTK Public Methods

        /// <summary>
        /// Convert a String into a SecureString and attempt to remove the original String from RAM
        /// </summary>
        /// <param name="toConvert">This is the String to convert into a SecureString, please note that String ToConvert will be Overwritten with the same length of *'s to try remove it from RAM so after conversion it can be used no more and the SecureString must be used instead</param>
        /// <returns>A SecureString which resides in memory in an encrypted state</returns>
        public static SecureString StringToSecureString(ref String toConvert)
        {
            SecureString outputSecureString = new SecureString();
            String overwriteString = String.Empty;

            foreach (Char c in toConvert)
            {
                outputSecureString.AppendChar(c);
            }

            //Overwrite original String to try clear it from RAM (Note may still reside in Paged and Hiberfil)
            for (int i = 0; i < toConvert.Length; i++)
            {
                overwriteString += "*";
            }

            toConvert = overwriteString;

            return outputSecureString;
        }

        /// <summary>
        /// Crypto Randomly generates a byte array that can be used safely as salt
        /// </summary>
        /// <param name="saltLength">Length of the generated byte array</param>
        /// <returns>A Byte Array to be used as Salt</returns>
        public static Byte[] GetRandomSalt(Int32 saltLength)
        {
            return PGenerateRandomSalt(saltLength);
        }

        /// <summary>
        /// Crypto Randomly generates a Byte Array that can be used safely as salt
        /// </summary>
        /// <returns>A Byte Array to be used as Salt</returns>
        public static Byte[] GetRandomSalt()
        {
            return PGenerateRandomSalt(CDefaultSaltLength);
        }

        /// <summary>
        ///  Input a password and a hash value in bytes and it uses PBKDF2 HMACSHA512 to hash the password and compare it to the supplied hash, uses PWDTK default iterations (cDefaultIterationCount)
        /// </summary>
        /// <param name="password">The text password to be hashed for comparrison</param>
        /// <param name="salt">The salt to be added to the  password pre hash</param>
        /// <param name="hash">The existing hash byte array you have stored for comparison</param>
        /// <returns>True if Password matches Hash else returns  false</returns>
        public static Boolean ComparePasswordToHash(Byte[] salt, String password, Byte[] hash)
        {
            if (PPasswordToHash(salt, StringToUtf8Bytes(password), CDefaultIterationCount).SequenceEqual(hash))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Input a password and a hash value in bytes and it uses PBKDF2 HMACSHA512 to hash the password and compare it to the supplied hash
        /// </summary>
        /// <param name="password">The text password to be hashed for comparrison</param>
        /// <param name="salt">The salt to be added to the  password pre hash</param>
        /// <param name="hash">The existing hash byte array you have stored for comparison</param>
        /// <param name="iterationCount">The number of times you have specified to hash the password for key stretching</param>
        /// <returns>True if Password matches Hash else returns  false</returns>
        public static Boolean ComparePasswordToHash(Byte[] salt, String password, Byte[] hash, Int32 iterationCount)
        {
            if (PPasswordToHash(salt, StringToUtf8Bytes(password), iterationCount).SequenceEqual(hash))
            {
                return true;
            }

            return false;
        }
      
        /// <summary>
        ///  Converts Salt + Password into a Hash using PWDTK defined default iterations (cDefaultIterationCount)
        /// </summary>
        /// <param name="salt">The salt to add infront of the password before processing the hash (Anti-Rainbow Table tactic)</param>
        /// <param name="password">The password used to compute the hash</param>
        /// <returns>The Hash value of the salt + password as a Byte Array</returns>
        public static Byte[] PasswordToHash(Byte[] salt, String password)
        {
            PCheckSaltCompliance(salt);

            Byte[] convertedPassword = StringToUtf8Bytes(password);

            PCheckPasswordSizeCompliance(convertedPassword);

            return PPasswordToHash(salt, convertedPassword , CDefaultIterationCount);
        }

        /// <summary>
        ///  Converts Salt + Password into a Hash
        /// </summary>
        /// <param name="salt">The salt to add infront of the password before processing the hash (Anti-Rainbow Table tactic)</param>
        /// <param name="password">The password used to compute the hash</param>
        /// <param name="iterationCount">Repeat the PBKDF2 dunction this many times (Anti-Rainbow Table tactic), higher value = more CPU usage which is better defence against cracking</param>
        /// <returns>The Hash value of the salt + password as a Byte Array</returns>
        public static Byte[] PasswordToHash(Byte[] salt, String password, Int32 iterationCount)
        {
            PCheckSaltCompliance(salt);

            Byte[] convertedPassword = StringToUtf8Bytes(password);

            PCheckPasswordSizeCompliance(convertedPassword);

            return PPasswordToHash(salt, convertedPassword, iterationCount);
        }

        /// <summary>
        /// Converts the Byte array Hash into a Human Friendly HEX String
        /// </summary>
        /// <param name="hash">The Hash value to convert</param>
        /// <returns>A HEX String representation of the Hash value</returns>
        public static String HashBytesToHexString(Byte[] hash)
        {
            return new SoapHexBinary(hash).ToString();
        }

        /// <summary>
        /// Converts the Hash Hex String into a Byte[] for computational processing
        /// </summary>
        /// <param name="hashHexString">The Hash Hex String to convert back to bytes</param>
        /// <returns>Esentially reverses the HashToHexString function, turns the String back into Bytes</returns>
        public static Byte[] HashHexStringToBytes(String hashHexString)
        {
           return SoapHexBinary.Parse(hashHexString).Value;
        }

        /// <summary>
        /// Tests the password for compliance against the supplied password policy
        /// </summary>
        /// <param name="password">The password to test for compliance</param>
        /// <param name="pwdPolicy">The PasswordPolicy that we are testing that the Password complies with</param>
        /// <returns>True for Password Compliance with the Policy</returns>
        public static Boolean TryPasswordPolicyCompliance(String password, PasswordPolicy pwdPolicy)
        {
            Boolean isCompliant = true;

            try
            {
                PCheckPasswordPolicyCompliance(password, pwdPolicy);
            }
            catch
            {
                isCompliant = false;
            }

            return isCompliant;
        }

        /// <summary>
        /// Tests the password for compliance against the supplied password policy
        /// </summary>
        /// <param name="password">The password to test for compliance</param>
        /// <param name="pwdPolicy">The PasswordPolicy that we are testing that the Password complies with</param>
        /// <param name="pwdPolicyException">The exception that will contain why the Password does not meet the PasswordPolicy</param>
        /// <returns>True for Password Compliance with the Policy</returns>
        public static Boolean TryPasswordPolicyCompliance(String password, PasswordPolicy pwdPolicy, ref PasswordPolicyException pwdPolicyException)
        {
            Boolean isCompliant = true;

            try
            {
                PCheckPasswordPolicyCompliance(password, pwdPolicy);
            }
            catch(PasswordPolicyException ex)
            {
                pwdPolicyException = ex;
                isCompliant = false;
            }

            return isCompliant;
        }

        /// <summary>
        /// Converts String to UTF8 friendly Byte Array
        /// </summary>
        /// <param name="stringToConvert">String to convert to Byte Array</param>
        /// <returns>A UTF8 decoded string as Byte Array</returns>
        public static Byte[] StringToUtf8Bytes(String stringToConvert)
        {
            return new UTF8Encoding(false).GetBytes(stringToConvert);
        }

        /// <summary>
        /// Converts UTF8 friendly Byte Array to String
        /// </summary>
        /// <param name="bytesToConvert">Byte Array to convert to String</param>
        /// <returns>A UTF8 encoded Byte Array as String</returns>
        public static String Utf8BytesToString(Byte[] bytesToConvert)
        {
            return new UTF8Encoding(false).GetString(bytesToConvert);
        }

        #endregion

        #region PWDTK Private Methods

        private static void PCheckPasswordPolicyCompliance(String password, PasswordPolicy pwdPolicy)
        {            
            if (new Regex(CNumbersRegex).Matches(password).Count<pwdPolicy.ForceXNumeric)
            {
                throw new PasswordPolicyException("The password must contain "+pwdPolicy.ForceXNumeric+" numeric [0-9] characters");
            }

            if (new Regex(CNonAlphaNumericRegex).Matches(password).Count < pwdPolicy.ForceXNonAlphaNumeric)
            {
                throw new PasswordPolicyException("The password must contain "+pwdPolicy.ForceXNonAlphaNumeric+" special characters");
            }

            if (new Regex(CUppercaseRegex).Matches(password).Count < pwdPolicy.ForceXUpperCase)
            {
                throw new PasswordPolicyException("The password must contain "+pwdPolicy.ForceXUpperCase+" uppercase characters");
            }

            if (password.Length < pwdPolicy.PasswordMinLength)
            {
                throw new PasswordPolicyException("The password does not have a length of at least "+pwdPolicy.PasswordMinLength+" characters");
            }

            if (password.Length > pwdPolicy.PasswordMaxLength)
            {
                throw new PasswordPolicyException("The password is longer than "+pwdPolicy.PasswordMaxLength+" characters");
            }
        }

        private static Byte[] PGenerateRandomSalt(Int32 saltLength)
        {
            Byte[] salt = new byte[saltLength];
            RandomNumberGenerator.Create().GetBytes(salt);
            return salt;
        }

        private static void PCheckSaltCompliance(Byte[] salt)
        {
            if (salt.Length < CMinSaltLength)
            {
                throw new SaltTooShortException("The supplied salt is too short, it must be at least " + CMinSaltLength + " bytes long as defined by CMinSaltLength");
            }
        }

        private static void PCheckPasswordSizeCompliance(Byte[] password)
        {
            if (password.Length > CMaxPasswordLength)
            {
                throw new PasswordTooLongException("The supplied password is longer than allowed, it must be smaller than " + CMaxPasswordLength + " bytes long as defined by CMaxPasswordLength");
            }
        }
        
        private static byte[] PPasswordToHash(Byte[] salt, Byte[] password, Int32 iterationCount)
        {
            return new Rfc2898(password, salt, iterationCount).GetDerivedKeyBytes_PBKDF2_HMACSHA512(CKeyLength);                 
        }

        #endregion
    }

    #region PWDTK Custom Exceptions

    public class SaltTooShortException : Exception
    {
        public SaltTooShortException(String message):base(message)
        {

        }
    }

    public class PasswordPolicyException : Exception
    {
        public PasswordPolicyException(String message):base(message)
        {
      
        }
    }

    public class PasswordTooLongException : Exception
    {
        public PasswordTooLongException(String message)
            : base(message)
        {

        }
    }

    #endregion
}
