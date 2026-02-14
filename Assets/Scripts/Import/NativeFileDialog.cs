using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UrbanScanVR.Import
{
    /// <summary>
    /// Нативный файловый диалог Windows через P/Invoke.
    /// Вызывает GetOpenFileName из comdlg32.dll.
    /// На других платформах возвращает null.
    /// </summary>
    public static class NativeFileDialog
    {
#if UNITY_STANDALONE_WIN

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool GetOpenFileName(ref OpenFileName ofn);

        /// <summary>
        /// Открывает стандартный Windows диалог выбора файла.
        /// Возвращает путь к выбранному файлу или null при отмене.
        /// </summary>
        /// <param name="title">Заголовок окна</param>
        /// <param name="filter">Фильтр файлов: "OBJ Files\0*.obj\0All Files\0*.*\0"</param>
        public static string OpenFile(string title = "Выберите OBJ файл",
            string filter = "OBJ Files\0*.obj\0All Files\0*.*\0")
        {
            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.hwndOwner = IntPtr.Zero;
            ofn.lpstrFilter = filter;
            ofn.lpstrFile = new string('\0', 260); // MAX_PATH
            ofn.nMaxFile = 260;
            ofn.lpstrTitle = title;
            ofn.Flags = 0x00001000  // OFN_FILEMUSTEXIST
                      | 0x00000800  // OFN_PATHMUSTEXIST
                      | 0x00080000; // OFN_EXPLORER

            if (GetOpenFileName(ref ofn))
            {
                // Убираем trailing null characters
                string path = ofn.lpstrFile.TrimEnd('\0');
                return path;
            }

            return null;
        }

#else
        /// <summary>Заглушка для не-Windows платформ</summary>
        public static string OpenFile(string title = "Выберите OBJ файл",
            string filter = "OBJ Files\0*.obj\0All Files\0*.*\0")
        {
            Debug.LogWarning("[NativeFileDialog] Файловый диалог поддерживается только на Windows");
            return null;
        }
#endif
    }
}
