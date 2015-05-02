﻿//The MIT License (MIT)
//
//Copyright (c) 2015 Fabian Fischer
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using IProxy.DataSerializing;
using IProxy;
using IProxy.common.data;
using Newtonsoft.Json;
using Ionic.Zlib;
using System.Reflection;

namespace MapRipper
{
    public class JsonMap
    {
        private XmlData dat;
        public JsonMap(XmlData dat)
        {
            this.dat = dat;
        }
        
        public string Name { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[][] Tiles { get; private set; }
        public ObjectDef[][][] Entities { get; private set; }

        public void Init(int w, int h, string name)
        {
            Width = w; Height = h;
            Tiles = new int[w][];
            Name = name;
            for (int i = 0; i < w; i++) Tiles[i] = new int[h];

            for (int w_ = 0; w_ < w; w_++)
                for (int h_ = 0; h_ < h; h_++)
                    Tiles[w_][h_] = -1;

            Entities = new ObjectDef[w][][];
            for (int i = 0; i < w; i++)
            {
                Entities[i] = new ObjectDef[h][];
                for (int j = 0; j < h; j++)
                {
                    Entities[i][j] = new ObjectDef[0];
                }
            }
        }

        private struct obj
        {
            public string name;
            public string id;
        }
        private struct loc
        {
            public string ground;
            public obj[] objs;
            public obj[] regions;
        }
        private struct json_dat
        {
            public byte[] data;
            public int width;
            public int height;
            public loc[] dict;
        }

        public string ToJson()
        {
            var obj = new json_dat();
            obj.width = Width; obj.height = Height;
            List<loc> locs = new List<loc>();
            MemoryStream ms = new MemoryStream();
            using (DWriter wtr = new DWriter(ms))
                for (int y = 0; y < obj.height; y++)
                    for (int x = 0; x < obj.width; x++)
                    {
                        var loc = new loc();
                        loc.ground = Tiles[x][y] != -1 ? dat.TileTypeToId[(ushort)Tiles[x][y]] : String.Empty;
                        loc.objs = new obj[Entities[x][y].Length];
                        for (int i = 0; i < loc.objs.Length; i++)
                        {
                            var en = Entities[x][y][i];
                            obj o = new obj()
                            {
                                id = dat.ObjectTypeToId[en.ObjectType]
                            };
                            string s = "";
                            Dictionary<StatsType, object> vals = new Dictionary<StatsType, object>();
                            foreach (var z in en.Stats.Stats) vals.Add(z.Key, z.Value);
                            if (vals.ContainsKey(StatsType.Name))
                                s += ";name:" + vals[StatsType.Name];
                            if (vals.ContainsKey(StatsType.Size))
                                s += ";size:" + vals[StatsType.Size];
                            if (vals.ContainsKey(StatsType.ObjectConnection))
                                s += ";conn:0x" + ((int)vals[StatsType.ObjectConnection]).ToString("X8");
                            if (vals.ContainsKey(StatsType.MerchantMerchandiseType))
                                s += ";mtype:" + vals[StatsType.MerchantMerchandiseType];
                            if (vals.ContainsKey(StatsType.MerchantRemainingCount))
                                s += ";mcount:" + vals[StatsType.MerchantRemainingCount];
                            if (vals.ContainsKey(StatsType.MerchantRemainingMinute))
                                s += ";mtime:" + vals[StatsType.MerchantRemainingMinute];
                            if (vals.ContainsKey(StatsType.NameChangerStar))
                                s += ";nstar:" + vals[StatsType.NameChangerStar];
                            o.name = s.Trim(';');
                            loc.objs[i] = o;
                        }

                        int ix = -1;
                        for (int i = 0; i < locs.Count; i++)
                        {
                            if (locs[i].ground != loc.ground) continue;
                            if (!((locs[i].objs != null && loc.objs != null) ||
                              (locs[i].objs == null && loc.objs == null))) continue;
                            if (locs[i].objs != null)
                            {
                                if (locs[i].objs.Length != loc.objs.Length) continue;
                                bool b = false;
                                for (int j = 0; j < loc.objs.Length; j++)
                                    if (locs[i].objs[j].id != loc.objs[j].id ||
                                        locs[i].objs[j].name != loc.objs[j].name)
                                    {
                                        b = true;
                                        break;
                                    }
                                if (b)
                                    continue;
                            }
                            ix = i;
                            break;
                        }
                        if (ix == -1)
                        {
                            ix = locs.Count;
                            locs.Add(loc);
                        }
                        wtr.Write((short)ix);
                    }
            obj.data = ZlibStream.CompressBuffer(ms.ToArray());
            obj.dict = locs.ToArray();
            var settings = new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Ignore;
            return JsonConvert.SerializeObject(obj, settings);
        }
    }
}