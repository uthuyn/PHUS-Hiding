using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    class IndexChainList
    {
        IList<int> seqIDList;

        public IList<int> SeqIDList
        {
            get { return seqIDList; }
            set { seqIDList = value; }
        }
        IList<IndexChain> listOfUtilityChain;

        public IList<IndexChain> ListOfUtilityChain
        {
            get { return listOfUtilityChain; }
            set { listOfUtilityChain = value; }
        }
       public IndexChainList() { }
       public IndexChainList(IList<int> seqIDList, IList<IndexChain> listOfUtilityChain)
        {
            this.seqIDList = seqIDList;
            this.listOfUtilityChain = listOfUtilityChain;
        }
    }
