using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CSDiscordTelegramLink
{
    public class TicketManager : IDisposable
    {
        public ConcurrentQueue<long> Tickets = new();
        private long Last = 0;
        private bool Running = true;

        /// <summary>
        /// In seconds
        /// </summary>
        public double DelayBetweenTickets { get; set; }

        public TicketManager(double delay)
        {
            DelayBetweenTickets = delay;

            var t = new Thread(Ticker);
            t.Start();
        }

        public void Ticker()
        {
            while (Running)
            {
                Thread.Sleep(1500);
                if (Tickets.Count > 0)
                {
                    Logger.Log(Tickets.Count);
                }
                if (!Running) { break; }
                Thread.Sleep(1500);
            }
        }

        public void WaitForTurn()
        {
            var id = Last++;
            Tickets.Enqueue(id);
            try
            {
                while (true)
                {
                    if (Tickets.Count == 0) { break; }

                    if (!Tickets.TryPeek(out var a) || a != id)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        break;
                    }
                }

                Thread.Sleep((int)(1000 * DelayBetweenTickets));
            }
            finally
            {
                if (!(Tickets.TryPeek(out var a) && a == id && Tickets.TryDequeue(out _)))
                {
                    try
                    {
                        Logger.Log("Dequed failed ticket: " + id);
                    }
                    catch { }
                }
            }
        }

        public void Dispose()
        {
            Running = false;
            Tickets.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
