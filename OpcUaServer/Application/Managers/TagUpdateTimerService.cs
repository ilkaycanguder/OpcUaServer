using Microsoft.AspNetCore.Razor.TagHelpers;
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpcUaServer.Application.Managers
{
    public class TagUpdateTimerService
    {
        private readonly CustomNodeManager2 _nodeManager;
        private readonly int _intervalMs;
        private Timer _timer;
        private bool _isRunning = false;

        public TagUpdateTimerService(CustomNodeManager2 nodeManager, int intervalSeconds = 5)
        {
            _nodeManager = nodeManager;
            _intervalMs = intervalSeconds * 1000;
        }

        public void Start()
        {
            if (_isRunning)
            {
                Stop(); 
            }

            _timer = new Timer(UpdateTagValues, null, 0, _intervalMs);
            _isRunning = true;
            Console.WriteLine($"⏱️ TagUpdateTimerService başlatıldı. {_intervalMs / 1000} sn aralıklarla değer güncellenecek.");
        }
        private void UpdateTagValues(object state)
        {
            try
            {
                var tagNodes = (_nodeManager as MyNodeManager)?.GetAllTagNodes();

                if (tagNodes == null || !tagNodes.Any())
                    return;

                foreach (var node in tagNodes)
                {
                    if (node.DataType == DataTypeIds.Int32)
                    {
                        int newValue = new Random().Next(0, 100);
                        node.Value = newValue;
                        node.Timestamp = DateTime.UtcNow;
                        node.ClearChangeMasks(_nodeManager.SystemContext, true);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"🔁 [Tag Güncellendi] {node.DisplayName} = {newValue}");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TagUpdateTimerService hatası: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite); 
                _timer.Dispose();
                _timer = null;
                _isRunning = false;
                Console.WriteLine("TagUpdateTimerService durduruldu.");
            }
        }
    }
}
