using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace JobManagerService
{
    public class DropFileManager
    {
        private Dictionary<string, DropFile> map;

        public DropFileManager()
        {
            map = new Dictionary<string, DropFile>();
        }

        public string GetDropFilePath(string uniqueFileName, string filePath)
        {
            if (map.ContainsKey(uniqueFileName))
            {
                DropFile df = map[uniqueFileName];
                df.UseCount++;
                return df.UNCFilePath;
            }
            else
            {
                string destFile = Path.Combine(AppConfig.FileDrop.FullName, Guid.NewGuid().ToString() + Path.GetExtension(filePath));
                System.IO.File.Copy(filePath, destFile);
                DropFile df = new DropFile();
                df.UNCFilePath = destFile;
                df.UseCount = 1;
                map[uniqueFileName] = df;
                return destFile;
            }
        }

        public void ReleaseDropFile(string uncFilePath)
        {
            string dropFileNameToDelete = null;
            foreach (string key in map.Keys)
            {
                DropFile df = map[key];
                if (df.UNCFilePath.ToLower() == uncFilePath.ToLower())
                {
                    df.UseCount--;
                    if (df.UseCount == 0)
                    {
                        dropFileNameToDelete = key;
                    }
                    break;
                }
            }
            if (dropFileNameToDelete != null)
            {
                DropFile df = map[dropFileNameToDelete];

                //delete the drop copy
                System.IO.File.Delete(df.UNCFilePath);

                map.Remove(dropFileNameToDelete);
            }
        }
    }

    public class DropFile
    {
        public string UNCFilePath { get; set; }
        public int UseCount { get; set; }
    }
}
