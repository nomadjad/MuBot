﻿namespace MuLibrary.Services.Items
{
    public class Item : ILibraryObject
    {
        public string Name          { get; set; }
        public string ItemType      { get; set; }
        public string ImageUrl      { get; set; }
        public string LibraryUrl    { get; set; }
    }
}
