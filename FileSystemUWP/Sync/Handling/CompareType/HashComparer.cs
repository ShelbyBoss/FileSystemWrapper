﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Storage;

namespace FileSystemUWP.Sync.Handling.CompareType
{
    class HashComparer : ISyncFileComparer
    {
        public new bool Equals(object obj1, object obj2)
        {
            return obj1 is string value1 && obj2 is string value2 && value1 == value2;
        }

        public Task<object> GetLocalCompareValue(StorageFile localFile)
        {
            return GetFileHash(localFile);
        }

        private static async Task<object> GetFileHash(StorageFile file)
        {
            using (SHA1 hashing = SHA1.Create())
            {
                using (Stream stream = await file.OpenStreamForReadAsync())
                {
                    return Convert.ToBase64String(hashing.ComputeHash(stream));
                }
            }
        }

        public async Task<object> GetServerCompareValue(string serverFilePath, Api api)
        {
            return await api.GetFileHash(serverFilePath);
        }
    }
}
