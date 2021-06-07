using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MagazinePatcher
{
    public static class PatcherStatus
    {
        public static float PatcherProgress { get => patcherProgress; }

        private static float patcherProgress = 0;

        public static void UpdateProgress(float progress)
        {
            patcherProgress = progress;
        }

    }
}
