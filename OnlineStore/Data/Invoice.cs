﻿using System;

namespace OnlineStore.Data
{
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime Date { get; set; }
        public Order Order { get; set; }
    }
}
