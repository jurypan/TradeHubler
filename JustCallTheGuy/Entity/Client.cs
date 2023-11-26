﻿using System.ComponentModel.DataAnnotations;

namespace JustCallTheGuy
{
    public class Client
    {
        public Client()
        {
            DateCreated = DateTime.UtcNow;
            Trades = [];
        }

        [Key]
        public int ID { get; set; }
        public DateTime DateCreated { get; set; }
        public Account? Account { get; set; }
        public int AccountID { get; set; }
        public required string Name { get; set; }
        public List<Trade> Trades { get; set; }
    }
}
