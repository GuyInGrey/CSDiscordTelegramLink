using System.Collections.Concurrent;
using System.Threading;

namespace CSDiscordTelegramLink
{
    public class TicketManager
    {
        private ConcurrentQueue<long> Tickets = new();
        private long Last = 0;

        /// <summary>
        /// In seconds
        /// </summary>
        public double DelayBetweenTickets { get; set; }

        public TicketManager(double delay)
        {
            DelayBetweenTickets = delay;
        }

        public void WaitForTurn()
        {
            var id = Last++;
            Tickets.Enqueue(id);
            try
            {
                while (true)
                {
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
    }
}
