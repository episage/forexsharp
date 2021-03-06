﻿using FXSharp.TradingPlatform.Exts;
using System.Linq;
namespace FXSharp.EA.NewsBox
{
    public class NewsShooterEA : EExpertAdvisor
    {
        private NewsReminder reminder;
        private OrderWatcherPool orderPool;
        private ICurrencyRepository currencyRepository;
        private bool initialized = false;

        protected override int DeInit()
        {
            reminder.Stop();
            return 0;
        }

        protected override int Init()
        {
            // should filter when the order is created. currently just do this simple things
            //CurrencyPairRegistry.FilterCurrencyForMinimalSpread(this);

            orderPool = new OrderWatcherPool();

            reminder = new NewsReminder();
            reminder.Start();

            currencyRepository = new MajorRelatedPairsRepository();
            initialized = true;
            return 0;
        }

        protected override int Start()
        {
            MakeSureAlreadyInitialized();

            while (reminder.IsAvailable)
            {
                MagicBoxOrder result = null;

                reminder.OrderQueue.TryDequeue(out result);

                if (result == null) continue;

                CreateOrderBox(result);
            }

            orderPool.ManageAllOrder();
            
            // 2. trailing stop and Lock Profit
            return 0;
        }

        private void MakeSureAlreadyInitialized()
        {
            if (initialized) return;

            Init();
        }

        private void CreateOrderBox(MagicBoxOrder magicBox)
        {
            /// need to refactor this messs into another class
            
            double range = magicBox.Range;
            double takeProfit = 0; // nullify take profit 
            double stopLoss = 0; // nullify stop loss, should set after enter the trade.
            double expiredTime = magicBox.MinuteExpiracy;
            
            var moneyManagement = new MoneyManagement(1, this.Balance);

            double lotSize = moneyManagement.CalculateLotSize(magicBox);
            
            foreach (var currencyPairs in currencyRepository.GetRelatedCurrencyPairs(this, magicBox.Symbol))
            {
                // check if the order has been created for this pair

                var buyOrder = PendingBuy(currencyPairs, lotSize,
                            BuyOpenPriceFor(currencyPairs) + range * PointFor(currencyPairs));

                var sellOrder = PendingSell(currencyPairs, lotSize,
                            SellOpenPriceFor(currencyPairs) - range * PointFor(currencyPairs));

                orderPool.Add(new OrderWatcher(buyOrder, sellOrder, expiredTime, magicBox.Config));    
            }
        }
    }
}
