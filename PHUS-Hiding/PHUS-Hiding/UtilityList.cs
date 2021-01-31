﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
public class UtilityList
{
    List<int> tid;
    public List<int> TID
    {
        get { return tid; }
        set { tid = value; }
    }

    float acu;
    public float ACU
    {
        get { return acu; }
        set { acu = value; }
    }
    float ru;
    public float RU
    {
        get { return ru; }
        set { ru = value; }
    }
    UtilityList link;
    public UtilityList Link
    {
        get { return link; }
        set { link = value; }
    }
    public UtilityList(List<int> tid, float acu, float ru, UtilityList link = null)
    {
        this.tid = tid;
        this.acu = acu;
        this.ru = ru;
        this.link = link;
    }
}