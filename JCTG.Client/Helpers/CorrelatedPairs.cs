using JCTG.Models;

namespace JCTG.Client
{
    public class CorrelatedPairs
    {
        public static bool IsNotCorrelated(string tickerToOpen, string typeToOpen, List<string> correlatedPairs, Dictionary<long, Order> openOrders)
        {
            // Do null reference check
            if(tickerToOpen != null && typeToOpen != null && correlatedPairs != null && openOrders != null) 
            {
                // Check if there is any open order whose symbol and type match the corrselatedPairs list and the typeToOpen respectively
                foreach (var order in openOrders.Select(f => f.Value))
                {
                    if (order.Symbol != null && order.Type != null && correlatedPairs.Contains(order.Symbol) && order.Type.Equals(typeToOpen, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // Found an open order that is in the correlated pairs list and matches the type
                    }
                }
            }

            return true; // No correlated open orders found
        }

        public static string GetCorrelatedPair(string tickerToOpen, string typeToOpen, List<string> correlatedPairs, Dictionary<long, Order> openOrders)
        {
            // Do null reference check
            if (tickerToOpen != null && typeToOpen != null && correlatedPairs != null && openOrders != null)
            {
                // Check if there is any open order whose symbol and type match the corrselatedPairs list and the typeToOpen respectively
                foreach (var order in openOrders.Select(f => f.Value))
                {
                    if (order.Symbol != null && order.Type != null && correlatedPairs.Contains(order.Symbol) && order.Type.Equals(typeToOpen, StringComparison.OrdinalIgnoreCase))
                    {
                        return order.Symbol; // Found an open order that is in the correlated pairs list and matches the type
                    }
                }
            }

            return string.Empty; // No correlated open orders found
        }
    }
}
