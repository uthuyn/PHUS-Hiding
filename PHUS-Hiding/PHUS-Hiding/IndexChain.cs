using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class IndexChain
{
    int seqID;
    public int SeqID
    {
        get { return seqID; }
        set { seqID = value; }
    }
    float peuts;
    public float PEUTS
    {
        get { return peuts; }
        set { peuts = value; }
    }

    IndexList idxList;
    public IndexList IndexList
    {
        get { return idxList; }
        set { idxList = value; }
    }
    float maxUtility;
    public float MaxUtility
    {
        get { return maxUtility; }
        set { maxUtility = value; }
    }
    public IndexChain(int seqID, float peuts, float maxUtility, IndexList idxList)
    {
        this.seqID = seqID;
        this.peuts = peuts;
        this.maxUtility = maxUtility;
        this.idxList = idxList;
    }
}
