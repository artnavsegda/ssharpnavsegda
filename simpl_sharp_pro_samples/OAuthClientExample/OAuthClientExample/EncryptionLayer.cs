using System.Text;
using Crestron.SimplSharp.Cryptography;

namespace OAuthClientExample
{
    /// <summary>
    /// EncryptionLayer encrypts and decrypts the Client Secret and Refresh Token
    /// when these two credentials are transferred between the Crestron DataStore
    /// and working memory. The encryption is accomplished using an RSA key pair stored in the
    /// Key Container provided by the CspParameters class
    /// 
    /// NOTE: A symmetric key algorithm would much more efficient for this purpose. However, 
    /// since the CspParameters object can only be passed to the constructor of asymmetric 
    /// crypto service providers, such as RSACryptoServiceProvider, I needed to use an
    /// asymmetric algorithm in order to get access to a persistent Key Container.
    /// </summary>
    public class EncryptionLayer
    {
        // OAuthKeyStore is the persistent key container that this OAuth client
        // uses to store the RSA key pair. This key pair, in turn, is used to 
        // encrypt and decrypt the Client Secret and the Refresh Token
        const string KeyStoreName = "OAuthKeyStore";

        public byte[] RSAEncrypt(string plaintext)
        {
            // retrieve RSA key pair from key container
            var csp = new CspParameters();
            csp.KeyContainerName = KeyStoreName;

            var rsa = new RSACryptoServiceProvider(csp);

            // encrypt the inputted string, which has a default encoding
            byte[] cipherbytes = rsa.Encrypt(Encoding.Default.GetBytes(plaintext), false);

            // return the encrypted bytes
            return cipherbytes;
        }

        public string RSADecrypt(byte[] ciphertext)
        {
            // retrieve RSA key pair from key container
            var csp = new CspParameters();
            csp.KeyContainerName = KeyStoreName;

            var rsa = new RSACryptoServiceProvider(csp); 

            // decrypt the inputted byte array
            byte[] plainbytes = null;
            plainbytes = rsa.Decrypt(ciphertext, false);

            // convert the plaintext bytes back to a string and return it
            return Encoding.Default.GetString(plainbytes, 0, plainbytes.Length);
        }
    }
}
