using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using CodeNotary.ImmuDb.ImmudbProto;

namespace CodeNotary.ImmuDb
{
    internal class CryptoUtils
    {
        private static byte leaf_prefix = 0;
        private static byte node_prefix = 1;

        public static void Verify(Proof proof, Item item, Root root)
        {
            if (!entryDigest(item).ContentEqual(proof?.Leaf?.ToByteArray()))
            {
                throw new VerificationException("Proof does not verify!");
            }

            verifyInclusion(proof);

            if (root != null && root.Index > 0)
            {
                verifyConsistency(proof, root);
            }
        }

        private static void verifyInclusion(Proof proof)
        {
            var at = proof.At;
            var i = proof.Index;

            if (i > at || (at > 0 && proof.InclusionPath.Count == 0))
            {
                throw new VerificationException("Inclusion proof does not verify!");
            }

            byte[] finalArray = proof.Leaf.ToByteArray();

            foreach (var inclusionArray in proof.InclusionPath)
            {
                using (var stream = new MemoryStream())
                {
                    stream.WriteByte(node_prefix);

                    if (i % 2 == 0 && i != at)
                    {
                        stream.Write(finalArray);
                        stream.Write(inclusionArray.ToByteArray());
                    }
                    else
                    {
                        stream.Write(inclusionArray.ToByteArray());
                        stream.Write(finalArray);
                    }

                    finalArray = stream.ToArray().SHAHash();
                }

                i /= 2;
                at /= 2;
            }

            if (at != i || !finalArray.ContentEqual(proof.Root.ToByteArray()))
            {
                throw new VerificationException("Inclusion proof does not verify!");
            }
        }

        private static void verifyConsistency(Proof proof, Root root)
        {
            var proofIndex = proof.At;
            var rootIndex = root.Index;

            var firstHash = root.Root_.ToByteArray();
            var secondHash = proof.Root.ToByteArray();

            if (rootIndex == proofIndex && firstHash.ContentEqual(secondHash) && proof.ConsistencyPath.Count == 0)
            {
                return;
            }

            if (rootIndex >= proofIndex || proof.ConsistencyPath.Count == 0)
            {
                throw new VerificationException("Consistency proof does not verify!");
            }

            var consistencyHashes = new List<byte[]>();

            if (isPowerOfTwo(rootIndex + 1))
            {
                consistencyHashes.Add(firstHash);
            }

            foreach (var historyEntry in proof.ConsistencyPath)
            {
                consistencyHashes.Add(historyEntry.ToByteArray());
            }

            var fn = rootIndex;
            var sn = proofIndex;
            
            while (fn % 2 == 1)
            {
                fn >>= 1;
                sn >>= 1;
            }

            var fr = consistencyHashes[0];
            var sr = consistencyHashes[0];

            for (int step = 1; step < consistencyHashes.Count; step++)
            {
                if (sn == 0)
                {
                    throw new VerificationException("Consistency proof does not verify!");
                }

                using (var srStream = new MemoryStream())
                {
                    srStream.WriteByte(node_prefix);

                    var stepHash = consistencyHashes[step];

                    if (fn % 2 == 1 || fn == sn)
                    {
                        using (var fnStream = new MemoryStream())
                        {
                            fnStream.WriteByte(node_prefix);
                            fnStream.Write(stepHash);
                            fnStream.Write(fr);

                            fr = fnStream.ToArray().SHAHash();

                            srStream.Write(stepHash);
                            srStream.Write(sr);

                            sr = srStream.ToArray().SHAHash();
                        }

                        while (fn % 2 == 0 && fn != 0)
                        {
                            fn >>= 1;
                            sn >>= 1;
                        }
                    }
                    else
                    {
                        srStream.Write(sr);
                        srStream.Write(stepHash);
                        sr = srStream.ToArray().SHAHash();
                    }
                }

                fn >>= 1;
                sn >>= 1;
            }

            if (!fr.ContentEqual(firstHash) || !sr.ContentEqual(secondHash) || sn != 0)
            {
                throw new VerificationException("Consistency proof does not verify!");
            }
        }

        private static bool isPowerOfTwo(ulong n)
        {
            return (n != 0) && ((n & (n - 1)) == 0);
        }

        private static byte[] entryDigest(Item item)
        {
            using var stream = new MemoryStream();

            stream.WriteByte(leaf_prefix);

            var indexArray = BitConverter.GetBytes(item.Index);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(indexArray);
            }

            var keyLengthArray = BitConverter.GetBytes((ulong)item.Key.Length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(keyLengthArray);
            }

            stream.Write(indexArray);
            stream.Write(keyLengthArray);

            stream.Write(item.Key.ToByteArray());
            stream.Write(item.Value.ToByteArray());

            return stream.ToArray().SHAHash();
        }
    }

    internal static class Extensions
    {
        public static void Write(this Stream stream, byte[] array)
        {
            stream.Write(array, 0, array.Length);
        }

        public static bool ContentEqual(this byte[] array1, byte[] array2)
        {
            if (array1.Length != array2.Length)
            {
                return false;
            }

            int idx = 0;

            foreach (byte bt in array1)
            {
                if (bt != array2[idx++])
                {
                    return false;
                }
            }

            return true;
        }

        public static byte[] SHAHash(this byte[] array)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return sha256.ComputeHash(array);
        }
    }
}