using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SepDownloadManager
{
    public class DownloadQueue
    {
        readonly List<DownloadItem> items;
        readonly Timer checkTimer;

        public DownloadQueue(List<DownloadItem> items)
        {
            this.items = items;

            checkTimer = new Timer();
            checkTimer.Interval = 1500; // 1.5 seconds
            checkTimer.Tick += (s, e) => Tick();
            checkTimer.Start();
        }

        void Tick()
        {
            int active = items.Count(i => i.State == "Downloading");

            if (active >= AppSettings.MaxSimultaneous)
                return;

            // Waiting items, sorted by priority (High first)
            var waiting = items
                .Where(i => i.State == "In Queue" || i.State == "Waiting")
                .OrderByDescending(i => PriorityValue(i.Priority))
                .ToList();

            foreach (var item in waiting)
            {
                if (active >= AppSettings.MaxSimultaneous)
                    break;

                StartItem(item);
                active++;
            }
        }

        static int PriorityValue(string p)
        {
            switch (p)
            {
                case "High": return 3;
                case "Normal": return 2;
                case "Low": return 1;
                default: return 2;
            }
        }

        async void StartItem(DownloadItem item)
        {
            try
            {
                await item.StartAsync();
            }
            catch (OperationCanceledException) { }
            catch
            {
                item.State = "Error";
            }
        }
    }
}
