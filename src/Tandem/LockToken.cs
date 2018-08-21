using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tandem
{
    /// <summary>
    /// Represents a lock token.
    /// </summary>
    public struct LockToken
    {
        /// <summary>
        /// The empty lock token.
        /// </summary>
        public static readonly LockToken None = default(LockToken);

        private string _owner;

        #region Properties
        /// <summary>
        /// Gets the owner identifier, maximum length is 128 UTF8 bytes.
        /// </summary>
        public string Owner {
            get {
                return _owner ?? "";
            }
            set {
                if (Encoding.UTF8.GetByteCount(value) > 128)
                    throw new ArgumentOutOfRangeException("The owner cannot be large than 128 bytes once encoded");

                _owner = value;
            }
        }

        /// <summary>
        /// Gets the unique identifier for the lock token.
        /// </summary>
        public Guid UUID { get; set; }

        /// <summary>
        /// Gets if the lock token is valid.
        /// </summary>
        public bool IsValid {
            get {
                return UUID != Guid.Empty;
            }
        }
        #endregion

        /// <summary>
        /// Parses the lock token.
        /// </summary>
        /// <param name="str">The token string.</param>
        /// <exception cref="FormatException">Thrown if the token string is invalid.</exception>
        /// <returns>The lock token.</returns>
        public static LockToken Parse(string str) {
            if (TryParse(str, out LockToken token))
                return token;
            else
                throw new FormatException("The lock token format is invalid or unsupported");
        }

        /// <summary>
        /// Trys to parse the lock token.
        /// </summary>
        /// <param name="str">The token string.</param>
        /// <param name="token"></param>
        /// <returns>If the parsing was successful.</returns>
        public static bool TryParse(string str, out LockToken token) {
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(str))) {
                BinaryReader reader = new BinaryReader(ms);

                byte version = reader.ReadByte();

                if (version == 1) {
                    byte[] uuidBytes = reader.ReadBytes(16);
                    byte ownerLength = reader.ReadByte();

                    if (ownerLength > 128) {
                        token = default(LockToken);
                        return false;
                    }

                    byte[] ownerBytes = reader.ReadBytes(ownerLength);

                    token = new LockToken();
                    token.UUID = new Guid(uuidBytes);
                    token.Owner = Encoding.UTF8.GetString(ownerBytes, 0, ownerBytes.Length);
                    return true;
                } else {
                    token = default(LockToken);
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the string representation of this token.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            using (MemoryStream ms = new MemoryStream()) {
                BinaryWriter writer = new BinaryWriter(ms);
                writer.Write((byte)1);
                writer.Write(UUID.ToByteArray());
                writer.Write((byte)Encoding.UTF8.GetByteCount(_owner ?? ""));
                writer.Write(Encoding.UTF8.GetBytes(_owner ?? ""));
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// Creates a new lock token.
        /// </summary>
        /// <param name="uuid">The UUID.</param>
        /// <param name="owner">The owner.</param>
        public LockToken(Guid uuid, string owner) {
            if (uuid == Guid.Empty)
                throw new ArgumentException("The UUID argument cannot be empty");

            UUID = uuid;
            _owner = owner;
        }
    }
}
