using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
public class IndexList
{
    int[] idxs;
    public int[] Indexs
    {
        get { return idxs; }
        set { idxs = value; }
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
    IndexList link;
    public IndexList Link
    {
        get { return link; }
        set { link = value; }
    }
    public IndexList(int[] idxs, float acu, float ru, IndexList link = null)
    {
        this.idxs = idxs;
        this.acu = acu;
        this.ru = ru;
        this.link = link;
    }
}
