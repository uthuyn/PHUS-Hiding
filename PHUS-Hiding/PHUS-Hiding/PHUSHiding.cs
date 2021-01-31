using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
public class PHUSHiding
{
    /// <summary>
    /// if true, debugging information will be shown in the console </summary>
    readonly bool DEBUG = false;

    #region "Declaration Variable"
    int sequenceCount = 0;
    /// <summary>
    /// the time the algorithm started </summary>
    public long startTimestamp = 0;
    /// <summary>
    /// the time the algorithm terminated </summary>
    public long endTimestamp = 0;
    /// <summary>
    /// record memory usage the algorithm terminated </summary>
    public Process currentProc;

    /// <summary>
    /// buffer for storing the current pattern that is mined when performing mining
    /// the idea is to always reuse the same buffer to reduce memory usage. *
    /// </summary>
    readonly int BUFFERS_SIZE = 30000;
    private int[] patternBuffer = null;

    /// <summary>
    /// the minUtility threshold * </summary>
    float minUtility = 0;

    /// <summary>
    /// max pattern length * </summary>
    int maxPatternLength = int.MaxValue;

    /// <summary>
    /// the input file path * </summary>
    string input;
    /// <summary>
    /// database file
    /// </summary>
    Dictionary<int, QMatrixHUSSpan> database;

    // create a map to store the SWU of each item
    // key: item  value: the swu of the item
    Dictionary<int, float> mapItemToSWU;
    //List to save High utility pattern 
    public Dictionary<int[], float> highUtilitySet = new Dictionary<int[], float>();

    Dictionary<int, float> externalData;
    /// <summary>
    /// the number of HUSP generated </summary>
    public int PHUSPCount { get; internal set; } = 0;
    public double HidingTotalTime { get; internal set; }
    public float DbDelta { get; internal set; }

    #endregion

    #region "Load data with internal and external"

    public Dictionary<int, QMatrixHUSSpan> loadDataWithInternalExternal(string external, string input, float minUtility)
    {
        //Read External value
        externalData = new Dictionary<int, float>();
        System.IO.StreamReader externalInput = null;
        string line;
        externalInput = new System.IO.StreamReader(new System.IO.FileStream(external, System.IO.FileMode.Open, System.IO.FileAccess.Read));
        // for each line (transaction) until the end of file
        while ((line = externalInput.ReadLine()) != null)
        {
            int position_Colons = line.IndexOf(':');
            string key = line.Substring(0, position_Colons);
            int item = int.Parse(key);
            string value = line.Substring(position_Colons + 1, line.Length - (position_Colons + 1));
            float price = float.Parse(value);
            externalData.Add(item, price);
        }
        this.input = input;

        // initialize the buffer for storing the current itemset
        patternBuffer = new int[BUFFERS_SIZE];

        // save the minimum utility threshold
        this.minUtility = minUtility;

        // create a map to store the SWU of each item
        // key: item  value: the swu of the item
        mapItemToSWU = new Dictionary<int, float>();

        // ==========  FIRST DATABASE SCAN TO IDENTIFY PROMISING ITEMS =========
        // We scan the database a first time to calculate the SWU of each item.
        System.IO.StreamReader myInput = null;
        string thisLine;
        // prepare the object for reading the file
        myInput = new System.IO.StreamReader(new System.IO.FileStream(input, System.IO.FileMode.Open, System.IO.FileAccess.Read));
        // for each line (transaction) until the end of file
        while ((thisLine = myInput.ReadLine()) != null)
        {
            // if the line is a comment, is  empty or is a kind of metadata, skip it
            if (thisLine.Length == 0 || thisLine[0] == '#' || thisLine[0] == '%' || thisLine[0] == '@')
            {
                continue;
            }
            // split the transaction according to the " " separator
            string[] tokens = thisLine.Split(" ", true);

            // get the sequence utility (the last token on the line)
            string sequenceUtilityString = tokens[tokens.Length - 1];
            int positionColons = sequenceUtilityString.IndexOf(':');
            float sequenceUtility = float.Parse(sequenceUtilityString.Substring(positionColons + 1));
            // Then read each token from this sequence (except the last three tokens
            // which are -1 -2 and the sequence utility)
            for (int i = 0; i < tokens.Length - 3; i++)
            {
                string currentToken = tokens[i];
                // if the current token is not -1 
                if (currentToken.Length != 0 && currentToken[0] != '-')
                {
                    // find the left brack
                    int positionLeftBracketString = currentToken.IndexOf('[');
                    // get the item
                    string itemString = currentToken.Substring(0, positionLeftBracketString);
                    int item = int.Parse(itemString);

                    // get the current SWU of that item
                    float swu = 0;
                    if (mapItemToSWU.ContainsKey(item))
                    {
                        swu = mapItemToSWU[item];
                    }
                    // add the utility of sequence utility to the swu of this item
                    if (swu == 0)
                        swu = sequenceUtility;
                    else
                        swu += sequenceUtility;
                    mapItemToSWU[item] = swu;
                }
            }

            // increase sequence count
            sequenceCount++;
        }
        //================  SECOND DATABASE SCAN ===================
        // Read the database again to create QMatrix for each sequence
        database = new Dictionary<int, QMatrixHUSSpan>(sequenceCount);
        //try
        //{
        // prepare the object for reading the file
        myInput = new System.IO.StreamReader(new System.IO.FileStream(input, System.IO.FileMode.Open, System.IO.FileAccess.Read));

        // We will read each sequence in buffers.
        // The first buffer will store the items of a sequence and the -1 between them)
        int[] itemBuffer = new int[BUFFERS_SIZE];
        // The second buffer will store the utility of items in a sequence and the -1 between them)
        float[] utilityBuffer = new float[BUFFERS_SIZE];
        // The following variable will contain the length of the data stored in the two previous buffer
        int itemBufferLength;
        // Finally, we create another buffer for storing the items from a sequence without
        // the -1. This is just used so that we can collect the list of items in that sequence
        // efficiently. We will use this information later to create the number of rows in the
        // QMatrix for that sequence.
        int[] itemsSequenceBuffer = new int[BUFFERS_SIZE];
        // The following variable will contain the length of the data stored in the previous buffer
        int itemsLength;
        //The following variable will store the sequence ID Clark Dinh
        int seqID = -1;
        // for each line (transaction) until the end of file
        while ((thisLine = myInput.ReadLine()) != null)
        {
            // if the line is  a comment, is  empty or is a kind of metadata
            if (thisLine.Length == 0 || thisLine[0] == '#' || thisLine[0] == '%' || thisLine[0] == '@')
            {
                continue;
            }

            // We reset the two following buffer length to zero because
            // we are reading a new sequence.
            itemBufferLength = 0;
            itemsLength = 0;
            // split the sequence according to the " " separator
            string[] tokens = thisLine.Split(" ", true);

            // get the sequence utility (the last token on the line)
            string sequenceUtilityString = tokens[tokens.Length - 1];
            int positionColons = sequenceUtilityString.IndexOf(':');
            float sequenceUtility = float.Parse(sequenceUtilityString.Substring(positionColons + 1));

            // This variable will count the number of itemsets
            int nbItemsets = 1;
            // This variable will be used to remember if an itemset contains at least a promising item
            // (otherwise, the itemset will be empty).
            bool currentItemsetHasAPromisingItem = false;

            // Copy the current sequence in the sequence buffer.
            // For each token on the line except the last three tokens
            // (the -1 -2 and sequence utility).
            for (int i = 0; i < tokens.Length - 3; i++)
            {
                string currentToken = tokens[i];

                // if empty, continue to next token
                if (currentToken.Length == 0)
                {
                    continue;
                }

                // if the current token is -1
                if (currentToken.Equals("-1"))
                {
                    // It means that it is the end of an itemset.
                    // So we check if there was a promising item in that itemset
                    if (currentItemsetHasAPromisingItem)
                    {
                        // If yes, then we keep the -1, because
                        // that itemset will not be empty.

                        // We store the -1 in the respective buffers 
                        itemBuffer[itemBufferLength] = -1;
                        utilityBuffer[itemBufferLength] = -1;
                        // We increase the length of the data stored in the buffers
                        itemBufferLength++;

                        // we update the number of itemsets in that sequence that are not empty
                        nbItemsets++;
                        // we reset the following variable for the next itemset that 
                        // we will read after this one (if there is one)
                        currentItemsetHasAPromisingItem = false;
                    }
                }
                else
                {
                    // if  the current token is an item
                    //  We will extract the item from the string:
                    int positionLeftBracketString = currentToken.IndexOf('[');
                    int positionRightBracketString = currentToken.IndexOf(']');
                    string itemString = currentToken.Substring(0, positionLeftBracketString);
                    int item = int.Parse(itemString);
                    // We also extract the utility from the string:
                    string internalString = currentToken.Substring(positionLeftBracketString + 1, positionRightBracketString - (positionLeftBracketString + 1));
                    float itemUtility = int.Parse(internalString) * externalData[item];

                    // if the item is promising (its SWU >= minutility), then
                    // we keep it in the sequence
                    if (mapItemToSWU[item] >= minUtility)
                    {
                        // We remember that this itemset contains a promising item
                        currentItemsetHasAPromisingItem = true;

                        // We store the item and its utility in the buffers
                        // for temporarily storing the sequence
                        itemBuffer[itemBufferLength] = item;
                        utilityBuffer[itemBufferLength] = itemUtility;
                        itemBufferLength++;

                        // We also put this item in the buffer for all items of this sequence
                        itemsSequenceBuffer[itemsLength++] = item;
                    }
                    else
                    {
                        // if the item is not promising, we subtract its utility 
                        // from the sequence utility, and we do not add it to the buffers
                        // because this item will not be part of a high utility sequential pattern.
                        sequenceUtility -= itemUtility;
                    }
                }
            }

            // If the sequence utility is now zero, which means that the sequence
            // is empty after removing unpromising items, we don't keep it
            if (sequenceUtility == 0)
            {
                sequenceCount--;
                ++seqID;
                continue;
            }
            // Now, we sort the buffer for storing all items from the current sequence
            // in alphabetical order
            Array.Sort(itemsSequenceBuffer, 0, itemsLength);
            // but an item may appear multiple times in that buffer so we will
            // loop over the buffer to remove duplicates
            // This variable remember the last insertion position in the buffer:
            int newItemsPos = 0;
            // This variable remember the last item read in that buffer
            int lastItemSeen = -999;
            // for each position in that buffer
            for (int i = 0; i < itemsLength; i++)
            {
                // get the item
                int item = itemsSequenceBuffer[i];
                // if the item was not seen previously
                if (item != lastItemSeen)
                {
                    // we copy it at the current insertion position
                    itemsSequenceBuffer[newItemsPos++] = item;
                    // we remember this item as the last seen item
                    lastItemSeen = item;
                }
            }
            // Now we count the number of items in that sequence
            int nbItems = newItemsPos;

            // And we will create the Qmatrix for that sequence
            QMatrixHUSSpan matrix = new QMatrixHUSSpan(nbItems, --nbItemsets, itemsSequenceBuffer, newItemsPos, sequenceUtility);
            matrix.SeqID = ++seqID;  // New Modify by Clark Dinh
            matrix.NbItemsets = nbItemsets;
            // We add the QMatrix to the initial sequence database.
            database.Add(matrix.SeqID, matrix);

            // Next we will fill the matrix column by column
            // This variable will represent the position in the sequence
            int posBuffer = 0;
            // for each itemset (column)
            for (int itemset = 0; itemset < nbItemsets; itemset++)
            {
                // This variable represent the position in the list of items in the QMatrix
                int posNames = 0;
                // While we did not reach the end of the sequence
                while (posBuffer < itemBufferLength)
                {
                    // Get the item at the current position in the sequence
                    int item = itemBuffer[posBuffer];
                    // if it is an itemset separator, we move to next position in the sequence
                    if (item == -1)
                    {
                        posBuffer++;
                        break;
                    }
                    // else if it is the item that correspond to the next row in the matrix
                    else if (item == matrix.ItemNames[posNames])
                    {
                        // calculate the utility for this item/itemset cell in the matrix
                        float utility = utilityBuffer[posBuffer];
                        // We update the reamining utility by subtracting the utility of the
                        // current item/itemset
                        sequenceUtility -= utility;
                        // update the cell in the matrix
                        matrix.registerItem(posNames, itemset, utility, sequenceUtility);
                        // move to the next item in the matrix and in the sequence
                        posNames++;
                        posBuffer++;
                    }
                    else if (item > matrix.ItemNames[posNames])
                    {
                        // if the next item in the sequence is larger than the current row in the matrix
                        // it means that the item do not appear in that itemset, so we put a utility of 0
                        // for that item and move to the next row in the matrix.
                        matrix.registerItem(posNames, itemset, 0, sequenceUtility);
                        posNames++;
                    }
                    else
                    {
                        // Otherwise, we put a utility of 0 for the current row in the matrix and move
                        // to the next item in the sequence
                        matrix.registerItem(posNames, itemset, 0, sequenceUtility);
                        posBuffer++;
                    }
                }
            }

            // if in debug mode, we print the q-matrix that we have just built
            if (DEBUG)
            {
                Console.WriteLine(matrix.ToString());
                Console.WriteLine();
            }
        }
        return database;
    }
    #endregion
    //The method for creating the utility chain of 1-length sequence
    private void firstTime(int[] prefix, int prefixLength, Dictionary<int, QMatrixHUSSpan> database, float minPer, float maxPer, float minAvg, float maxAvg, string uid)
    {
        // For the first call to HUSSPan, we only need to check I-Extension
        // =======================  I-Extension  ===========================/  
        // For each item 
        //startTimestamp = DateTimeHelperClass.CurrentUnixTimeMillis();
        IDictionary<int, float> mapItemSWU = new Dictionary<int, float>();
        foreach (var seq in database)
        {
            QMatrixHUSSpan qmatrix = seq.Value;
            // for each row (item) we will update the swu of the corresponding item
            foreach (int item in qmatrix.ItemNames)
            {
                // get its swu
                float currentSWU = 0;
                // update its swu
                if (mapItemSWU.ContainsKey(item))
                {
                    currentSWU = mapItemSWU[item];
                    mapItemSWU[item] = currentSWU + qmatrix.Swu;
                }
                else
                {
                    mapItemSWU[item] = qmatrix.Swu;
                }
            }
        }
        foreach (KeyValuePair<int, float> entry in mapItemSWU.SetOfKeyValuePairs())
        {
            if (entry.Value >= minUtility)
            {
                // We get the item
                int item = entry.Key;
                // We initialize two variables for calculating the total utility and remaining utility
                // of that item => PEU(t) = totalUtility + totalRemainingUtility
                float peut = 0;
                float totalUtility = 0;

                // We also initialize a variable to remember the projected qmatrixes of sequences
                // where this item appears. This will be used for call to the recursive
                // "husspan" method later.

                IndexChainList utilityChains = new IndexChainList();
                IList<IndexChain> listofUtility1Chain = new List<IndexChain>();
                IList<int> seqIdList = new List<int>();
                // For each sequence
                foreach (var seq in database)
                {
                    QMatrixHUSSpan qmatrix = seq.Value;
                    //The following variable is used to create the utility chain of sequence t in each q-sequence
                    IndexList idxList = null;
                    IndexChain idxChain = null;
                    // This variable will store the maximum utility and maximum tid
                    float maxUtility = 0;
                    // if the item appear in that sequence (in that qmatrix)
                    int row = Array.BinarySearch(qmatrix.ItemNames, item);
                    if (row >= 0)
                    {
                        // for each itemset in that sequence
                        int itemset;
                        float maxpeuts = 0;
                        for (itemset = qmatrix.MatrixItemRemainingUtility[row].Length - 1; itemset >= 0; itemset--)
                        {
                            // get the utility of the item in that itemset
                            float utility = qmatrix.MatrixItemUtility[row][itemset];
                            // if the utility is higher than 0
                            if (utility > 0)
                            {
                                List<int> positionList = new List<int> { (row * qmatrix.NbItemsets) + itemset };
                                if (utility > maxUtility)
                                {
                                    maxUtility = utility;
                                }
                                float peuts = 0;
                                float remaining = qmatrix.MatrixItemRemainingUtility[row][itemset];
                                if (remaining == 0)
                                    peuts = 0;
                                else
                                    peuts = utility + remaining;
                                if (peuts > maxpeuts)
                                    maxpeuts = peuts;
                                if (idxList == null)
                                    idxList = new IndexList(positionList.ToArray(), utility, remaining);
                                else
                                {
                                    idxList = new IndexList(positionList.ToArray(), utility, remaining, idxList);
                                }
                            }
                        }
                        idxChain = new IndexChain(qmatrix.SeqID, maxpeuts, maxUtility, idxList);
                        seqIdList.Add(qmatrix.SeqID);
                        listofUtility1Chain.Add(idxChain);
                        // update the peut of 1-sequence t until now by adding the maxpeuts of the current sequence
                        peut += maxpeuts;
                        //Concurently we update the utility of this sequence
                        totalUtility += maxUtility;
                    }
                }
                if (checkPeriodicByIDList(seqIdList, maxPer))
                {
                    utilityChains = new IndexChainList(seqIdList, listofUtility1Chain);
                    // create the pattern consisting of this item
                    // by appending the item to the prefix in the buffer, which is empty
                    prefix[0] = item;
                    // if the pattern is high utility then output it
                    if (totalUtility >= minUtility)
                    {

                        if (checkFinalPeriodic(seqIdList, minPer, maxPer, minAvg, maxAvg))
                        {
                            PHUSPCount++;
                            int[] items = new int[1];
                            for (int i = 0; i < items.Length; i++)
                            {
                                items[i] = prefix[i];
                            }
                            highUtilitySet.Add(items, totalUtility);
                            PHUS_Hiding(items, listofUtility1Chain, totalUtility, uid);
                        }
                    }

                    //Then, we recursively call the procedure husspan for growing this pattern and
                    // try to find larger high utility sequential patterns

                    // if this item passes the depth pruning (remaining utility + totality >= minutil), i.e PEU(t) >=minutil
                    if (peut >= minUtility)
                    {
                        if (1 < maxPatternLength)
                        {
                            mining(prefix, 1, utilityChains, 1, minPer, maxPer, minAvg, maxAvg, uid);
                        }
                    }
                }
            }
        }
    }

    private void mining(int[] prefix, int prefixLength, IndexChainList idxChains, int itemCount, float minPer, float maxPer, float minAvg, float maxAvg, string uid)
    {
        // =======================  I-Extension  ===========================/
        // We first try to perform I-Extension to grow the pattern larger.
        // We scan the Utility Chain to find item that could be concatenated to the prefix.
        // For each sequence in the projected database
        IDictionary<int, float> iList = new Dictionary<int, float>();
        IDictionary<int, float> sList = new Dictionary<int, float>();
        int lastItem = prefix[prefixLength - 1];
        //First collecting all the items for i-list
        //And concurrently calculate the RSU(t,s) of all extension sequence
        IList<IndexChain> idxChainList = idxChains.ListOfUtilityChain;
        IList<int> seqIdList = idxChains.SeqIDList;
        IndexChain idxChain;
        for (int x = 0; x < idxChainList.Count; x++)
        {
            idxChain = idxChainList[x];
            // Get the utility list in the utility chain 
            IndexList idxList = idxChain.IndexList;
            QMatrixHUSSpan qmatrix = database[seqIdList[x]];
            //The two temporal list to check whether items are already in ilist and slist
            IList<int> iitems = new List<int>();
            IList<int> sitems = new List<int>();
            do
            {
                int icolumn = idxList.Indexs.Last() % qmatrix.NbItemsets;
                int irow = Array.BinarySearch(qmatrix.getItemNames(), lastItem) + 1;
                for (; irow < qmatrix.getItemNames().Length; irow++)
                {
                    // get the item for this row
                    int item = qmatrix.getItemNames()[irow];
                    float currentRSU;
                    float firstRSU;
                    // if the item appears in that column
                    if (qmatrix.getItemUtility(irow, icolumn) > 0)
                    {
                        if (!iitems.Contains(item))
                        {
                            iitems.Add(item);
                            if (iList.ContainsKey(item))
                            {
                                currentRSU = iList[item];
                                currentRSU += idxChain.PEUTS;
                                iList[item] = currentRSU;
                            }
                            // if it is the first time that we see this item
                            else
                            {
                                // We use a Pair object to store the SWU of the item and the
                                firstRSU = idxChain.PEUTS;
                                iList[item] = firstRSU;
                            }
                        }
                    }
                }

                // For each item
                for (int srow = 0; srow < qmatrix.getItemNames().Length; srow++)
                {
                    // get the item for this row
                    int item = qmatrix.getItemNames()[srow];
                    for (int scolumn = icolumn + 1; scolumn < qmatrix.MatrixItemUtility[srow].Length; scolumn++)
                    {
                        float currentRSU;
                        float firstRSU;
                        // if the item appears in that column
                        if (qmatrix.getItemUtility(srow, scolumn) > 0)
                        {
                            if (!sitems.Contains(item))
                            {
                                sitems.Add(item);
                                if (sList.ContainsKey(item))
                                {
                                    currentRSU = sList[item];
                                    currentRSU += idxChain.PEUTS;
                                    sList[item] = currentRSU;
                                }
                                // if it is the first time that we see this item
                                else
                                {
                                    // We use a Pair object to store the SWU of the item and the
                                    firstRSU = idxChain.PEUTS;
                                    sList[item] = firstRSU;
                                }
                            }
                            break;
                        }
                    }
                }

                idxList = idxList.Link;
            } while (idxList != null);

        }
        //// Now that we have calculated the local RSU of each item,
        ////We perform a loop on each item and for each promising item we will create
        ////the i-concatenation and calculate the utility of the resulting pattern.

        ////For each item
        foreach (KeyValuePair<int, float> entry in iList.SetOfKeyValuePairs())
        {
            // if the item is promising (RSU >= minutil)
            if (entry.Value >= minUtility)
            {
                // get the item
                int item = entry.Key;
                // we will traverse the utility chain to calculate the utility 
                // and create the extension utility chain of i-concatenation
                // We initialize two variables for calculating the total utility and PEUT
                // of that item => PEU(t) = totalUtility + totalRemainingUtility
                float peut = 0;
                float totalUtility = 0;
                // Initialize a variable to store the utility chain for the i-concatenation
                // of this item to the prefix
                IList<IndexChain> idxChainIConcat = new List<IndexChain>();
                IList<int> seqIdListIConcatenation = new List<int>();
                IndexChainList idxChainListIConcat = new IndexChainList();

                for (int y = 0; y < idxChainList.Count; y++)
                {
                    idxChain = idxChainList[y];
                    // Get the utility list in the utility chain 
                    IndexList idxList = idxChain.IndexList;
                    QMatrixHUSSpan qmatrix = database[seqIdList[y]];
                    // This variable will store the maximum utility and maximum peuts
                    float maxUtility = 0;
                    float maxpeuts = 0;
                    IndexChain exIdxChain = null;
                    //The following variable is used to create the utility list of sequence t in each q-sequence
                    IndexList finalList = null;
                    IList<IndexList> tmpList = new List<IndexList>();
                    int row = Array.BinarySearch(qmatrix.getItemNames(), item);
                    if (row >= 0)
                    {
                        do
                        {
                            int column = idxList.Indexs.Last() % qmatrix.NbItemsets;
                            // get the utility of the item in that itemset
                            float utility = qmatrix.MatrixItemUtility[row][column];
                            // if the utility is higher than 0
                            if (utility > 0)
                            {
                                List<int> positions = new List<int>(idxList.Indexs);

                                positions.Add((row * qmatrix.NbItemsets) + column);

                                float peuts = 0;
                                float acu = idxList.ACU + utility;
                                if (acu > maxUtility)
                                    maxUtility = acu;
                                float remaining = qmatrix.MatrixItemRemainingUtility[row][column];
                                if (remaining == 0)
                                    peuts = 0;
                                else
                                    peuts = acu + remaining;
                                if (peuts > maxpeuts)
                                    maxpeuts = peuts;
                                IndexList newList = new IndexList(positions.ToArray(), acu, remaining);
                                tmpList.Add(newList);
                            }
                            idxList = idxList.Link;
                        } while (idxList != null);
                    }
                    if (tmpList.Count > 0)
                    {
                        for (int i = 0; i < tmpList.Count - 1; i++)
                        {
                            tmpList[i].Link = tmpList[i + 1];
                        }
                        finalList = tmpList[0];
                        exIdxChain = new IndexChain(qmatrix.SeqID, maxpeuts, maxUtility, finalList);
                        idxChainIConcat.Add(exIdxChain);
                        seqIdListIConcatenation.Add(qmatrix.SeqID);
                    }
                    // update the peut of extension sequence t until now by adding the maxpeuts of the current sequence
                    peut += maxpeuts;
                    //Concurently we update the utility of this sequence
                    totalUtility += maxUtility;
                }

                if (checkPeriodicByIDList(seqIdListIConcatenation, maxPer))
                {
                    idxChainListIConcat = new IndexChainList(seqIdListIConcatenation, idxChainIConcat);

                    // create the i-concatenation by appending the item to the prefix in the buffer
                    prefix[prefixLength] = item;
                    // if the i-concatenation is high utility, then output it
                    if (totalUtility >= minUtility)
                    {
                        if (checkFinalPeriodic(seqIdListIConcatenation, minPer, maxPer, minAvg, maxAvg))
                        {
                            //writeOut(prefix, prefixLength + 1, totalUtility);
                            int[] items = new int[prefixLength + 1];
                            for (int i = 0; i < items.Length; i++)
                            {
                                items[i] = prefix[i];
                            }
                            highUtilitySet.Add(items, totalUtility);
                            PHUSPCount++;
                            PHUS_Hiding(items, idxChainIConcat, totalUtility, uid);
                        }
                    }
                    // Finally, we recursively call the procedure husspan for growing this pattern
                    // to try to find larger patterns
                    //if this i-concatenation passes the depth pruning (remaining utility + totality)
                    if (peut >= minUtility)
                    {
                        if (itemCount + 1 < maxPatternLength)
                        {
                            mining(prefix, prefixLength + 1, idxChainListIConcat, itemCount + 1, minPer, maxPer, minAvg, maxAvg, uid);
                        }
                    }
                }
            }
        }

        // =======================  S-Extension  ===========================/
        // We will next look for for S-Extension.
        // Next we will calculate the utility of each s-concatenation for promising 
        // items that can be appended by s-concatenation
        foreach (KeyValuePair<int, float> entry in sList.SetOfKeyValuePairs())
        {
            // if the item is promising (RSU >= minutil)
            if (entry.Value >= minUtility)
            {
                // get the item
                int item = entry.Key;
                // we will traverse the utility chain to calculate the utility 
                // and create the extension utility chain of s-concatenation
                // We initialize two variables for calculating the total utility and PEUT
                // of that item => PEU(t) = totalUtility + totalRemainingUtility
                float peut = 0;
                float totalUtility = 0;
                // Initialize a variable to store the utility chain for the s-concatenation
                // of this item to the prefix
                IList<IndexChain> idxChainSConcat = new List<IndexChain>();
                IList<int> seqIdListSConcatenation = new List<int>();
                IndexChainList idxChainListSConcat = new IndexChainList();

                for (int z = 0; z < idxChainList.Count; z++)
                {
                    idxChain = idxChainList[z];
                    // Get the utility list in the utility chain 
                    IndexList idxList = idxChain.IndexList;
                    QMatrixHUSSpan qmatrix = database[seqIdList[z]];
                    // This variable will store the maximum utility and maximum peuts
                    float maxUtility = 0;
                    float maxpeuts = 0;
                    IndexChain exIdxChain = null;
                    //The following variable is used to create the utility list of sequence t in each q-sequence
                    IndexList finalList = null;
                    IList<IndexList> tmpList = new List<IndexList>();
                    int row = Array.BinarySearch(qmatrix.getItemNames(), item);
                    if (row >= 0)
                    {
                        do
                        {
                            for (int column = (idxList.Indexs.Last() % qmatrix.NbItemsets) + 1; column < qmatrix.MatrixItemUtility[row].Length; column++)
                            {
                                // get the utility of the item in that itemset
                                float utility = qmatrix.MatrixItemUtility[row][column];
                                // if the utility is higher than 0
                                if (utility > 0)
                                {
                                    List<int> positions = new List<int>(idxList.Indexs);
                                    positions.Add((row * qmatrix.NbItemsets) + column);
                                    float peuts = 0;
                                    float acu = idxList.ACU + utility;
                                    if (acu > maxUtility)
                                        maxUtility = acu;
                                    float remaining = qmatrix.MatrixItemRemainingUtility[row][column];
                                    if (remaining == 0)
                                        peuts = 0;
                                    else
                                        peuts = acu + remaining;
                                    if (peuts > maxpeuts)
                                        maxpeuts = peuts;
                                    IndexList newList = new IndexList(positions.ToArray(), acu, remaining);
                                    tmpList.Add(newList);
                                }
                            }
                            idxList = idxList.Link;
                        } while (idxList != null);
                    }
                    if (tmpList.Count > 0)
                    {
                        for (int i = 0; i < tmpList.Count - 1; i++)
                        {
                            tmpList[i].Link = tmpList[i + 1];
                        }
                        finalList = tmpList[0];
                        exIdxChain = new IndexChain(qmatrix.SeqID, maxpeuts, maxUtility, finalList);
                        idxChainSConcat.Add(exIdxChain);
                        seqIdListSConcatenation.Add(qmatrix.SeqID);
                    }
                    // update the peut of extension sequence t until now by adding the maxpeuts of the current sequence
                    peut += maxpeuts;
                    //Concurently we update the utility of this sequence
                    totalUtility += maxUtility;
                }
                if (checkPeriodicByIDList(seqIdListSConcatenation, maxPer))
                {
                    idxChainListSConcat = new IndexChainList(seqIdListSConcatenation, idxChainSConcat);
                    // create ths s-concatenation by appending an itemset separator to 
                    // start a new itemset
                    prefix[prefixLength] = -1;
                    // then we append the new item
                    prefix[prefixLength + 1] = item;
                    // if this s-concatenation is high utility, then we output it
                    if (totalUtility >= minUtility)
                    {
                        if (checkFinalPeriodic(seqIdListSConcatenation, minPer, maxPer, minAvg, maxAvg))
                        {
                            int[] items = new int[prefixLength + 2];
                            for (int i = 0; i < items.Length; i++)
                            {
                                items[i] = prefix[i];
                            }
                            highUtilitySet.Add(items, totalUtility);
                            PHUSPCount++;
                            PHUS_Hiding(items, idxChainSConcat, totalUtility, uid);
                        }
                    }

                    // Finally, we recursively call the procedure husspan() for growing this pattern
                    // to try to find larger high utilit sequential patterns
                    //if this s-concatenation passes the depth pruning (remaining utility + totality)
                    if (peut >= minUtility)
                    {

                        if (itemCount + 1 < maxPatternLength)
                        {
                            mining(prefix, prefixLength + 2, idxChainListSConcat, itemCount + 1, minPer, maxPer, minAvg, maxAvg, uid);
                        }
                    }
                }
            }
        }
    }

    public void setMaxPatternLength(int maxPatternLength)
    {
        this.maxPatternLength = maxPatternLength;
    }
    public void runAlgorithm(Dictionary<int, QMatrixHUSSpan> database, string output, float minPer, float maxPer, float minAvg, float maxAvg, string uid)
    {
        highUtilitySet = new Dictionary<int[], float>();
        PHUSPCount = 0;
        // record the memory usage of the algorithm
        currentProc = Process.GetCurrentProcess();
        //record the start time of the algorithm
        //must do at begin
        //startTimestamp = DateTimeHelperClass.CurrentUnixTimeMillis();

        // create a writer object to write results to file
        //writer = new System.IO.StreamWriter(output);

        // Mine the database recursively using the husspan procedure
        // This procedure is the husspan procedure optimized for the first recursion
        firstTime(patternBuffer, 0, database, minPer, maxPer, minAvg, maxAvg, uid);

        // record end time
        endTimestamp = DateTimeHelperClass.CurrentUnixTimeMillis();
    }

    public void PHUS_Hiding(int[] items, IList<IndexChain> idxChains, float utility, string uid)
    {
        var start = DateTime.Now;
        Dictionary<int, Dictionary<string, string>> modifyDic = new Dictionary<int, Dictionary<string, string>>(idxChains.Count);
        List<int> itemList = new List<int>();
        for (int i = 0; i < items.Count(); i++)
        {
            if (items[i] != -1)
                itemList.Add(items[i]);
        }
        float diff = utility - minUtility;
        while (diff > 0)
        {
            //First Scanning to calculate the sum of each item in high utility pattern
            float[] sumIp = new float[itemList.Count];
            foreach (IndexChain idxChain in idxChains)
            {
                if (idxChain.IndexList != null)
                {
                    // Get the utility list in the utility chain 
                    IndexList idxList = idxChain.IndexList;
                    QMatrixHUSSpan qMatrix = database[idxChain.SeqID];
                    int[] idxs = new int[0];
                    do
                    {
                        if (idxList.ACU == idxChain.MaxUtility)
                        {
                            idxs = idxList.Indexs;
                            break;
                        }
                        else
                            idxList = idxList.Link;
                    } while (idxList != null);
                    if (idxs.Length > 0)
                    {
                        for (int i = 0; i < itemList.Count(); i++)
                        {
                            var row = idxs[i] / qMatrix.NbItemsets;
                            var column = idxs[i] % qMatrix.NbItemsets;
                            float itemUtility = qMatrix.MatrixItemUtility[row][column];
                            sumIp[i] += itemUtility;
                        }
                    }
                }
            }
            float uMax = sumIp.Max();
            int indexMax = -1;
            for (int i = indexMax + 1; i < sumIp.Count(); i++)
            {
                if (sumIp[i] == uMax)
                {
                    indexMax = i;
                    break;
                }
            }
            int itemMax = itemList[indexMax];

            int numberDecreasedItems = (int)Math.Ceiling((double)diff / externalData[itemMax]);
            float anpha = (float)(numberDecreasedItems * externalData[itemMax]) / uMax;
            //Second scanning to change quantity
            for (int k = 0; k < idxChains.Count; k++)
            {
                if (idxChains[k].IndexList != null)
                {
                    float reducingSum = 0;
                    QMatrixHUSSpan qMatrix = database[idxChains[k].SeqID];
                    // Get the utility list in the utility chain 
                    IndexList idxList = idxChains[k].IndexList;
                    int[] idxs = new int[0];
                    do
                    {
                        if (idxList.ACU == idxChains[k].MaxUtility)
                        {
                            idxs = idxList.Indexs;
                            break;
                        }
                        else
                            idxList = idxList.Link;
                    } while (idxList != null);
                    if (idxs.Length > 0)
                    {
                        int itemsetIndex = idxs[indexMax] % qMatrix.NbItemsets; //column
                        int row = Array.BinarySearch(qMatrix.getItemNames(), itemMax);
                        float itemUtility = qMatrix.MatrixItemUtility[row][itemsetIndex];
                        int subtractItem = (int)Math.Ceiling(anpha * (itemUtility / externalData[itemMax]));
                        if (itemUtility > subtractItem * externalData[itemMax])
                        {
                            qMatrix.MatrixItemUtility[row][itemsetIndex] -= subtractItem * externalData[itemMax];
                            diff -= subtractItem * externalData[itemMax];
                            reducingSum += subtractItem * externalData[itemMax];

                            //Update the remaining utility of qmatrix
                            if (itemsetIndex == 0)
                            {
                                for (int iRowUpdate = row - 1; iRowUpdate >= 0; iRowUpdate--)
                                {
                                    qMatrix.MatrixItemRemainingUtility[iRowUpdate][0] = qMatrix.MatrixItemUtility[iRowUpdate + 1][0] + qMatrix.MatrixItemRemainingUtility[iRowUpdate + 1][0];
                                }
                            }
                            else
                            {
                                int iColUpdate = itemsetIndex;
                                for (int iRowUpdate = row - 1; iRowUpdate >= 0; iRowUpdate--)
                                {
                                    qMatrix.MatrixItemRemainingUtility[iRowUpdate][iColUpdate] = qMatrix.MatrixItemUtility[iRowUpdate + 1][iColUpdate] + qMatrix.MatrixItemRemainingUtility[iRowUpdate + 1][iColUpdate];
                                }
                                iColUpdate--;
                                int rowLength = qMatrix.getItemNames().Length;
                                for (; iColUpdate >= 0; iColUpdate--)
                                {
                                    qMatrix.MatrixItemRemainingUtility[rowLength - 1][iColUpdate] = qMatrix.MatrixItemUtility[0][iColUpdate + 1] + qMatrix.MatrixItemRemainingUtility[0][iColUpdate + 1];
                                    for (int iRowUpdate = rowLength - 2; iRowUpdate >= 0; iRowUpdate--)
                                    {
                                        qMatrix.MatrixItemRemainingUtility[iRowUpdate][iColUpdate] = qMatrix.MatrixItemUtility[iRowUpdate + 1][iColUpdate] + qMatrix.MatrixItemRemainingUtility[iRowUpdate + 1][iColUpdate];
                                    }
                                }
                            }
                        }
                        else
                        {
                            var subUtl = itemUtility - externalData[itemMax];
                            diff -= subUtl;
                            reducingSum += subUtl;
                            qMatrix.MatrixItemUtility[row][itemsetIndex] = externalData[itemMax];

                            //Update the remaining utility of qmatrix
                            if (itemsetIndex == 0)
                            {
                                for (int iRowUpdate = row - 1; iRowUpdate >= 0; iRowUpdate--)
                                {
                                    qMatrix.MatrixItemRemainingUtility[iRowUpdate][0] = qMatrix.MatrixItemUtility[iRowUpdate + 1][0] + qMatrix.MatrixItemRemainingUtility[iRowUpdate + 1][0];
                                }
                            }
                            else
                            {
                                int iColUpdate = itemsetIndex;
                                for (int iRowUpdate = row - 1; iRowUpdate >= 0; iRowUpdate--)
                                {
                                    qMatrix.MatrixItemRemainingUtility[iRowUpdate][iColUpdate] = qMatrix.MatrixItemUtility[iRowUpdate + 1][iColUpdate] + qMatrix.MatrixItemRemainingUtility[iRowUpdate + 1][iColUpdate];
                                }
                                iColUpdate--;
                                int rowLength = qMatrix.getItemNames().Length;
                                for (; iColUpdate >= 0; iColUpdate--)
                                {
                                    qMatrix.MatrixItemRemainingUtility[rowLength - 1][iColUpdate] = qMatrix.MatrixItemUtility[0][iColUpdate + 1] + qMatrix.MatrixItemRemainingUtility[0][iColUpdate + 1];
                                    for (int iRowUpdate = rowLength - 2; iRowUpdate >= 0; iRowUpdate--)
                                    {
                                        qMatrix.MatrixItemRemainingUtility[iRowUpdate][iColUpdate] = qMatrix.MatrixItemUtility[iRowUpdate + 1][iColUpdate] + qMatrix.MatrixItemRemainingUtility[iRowUpdate + 1][iColUpdate];
                                    }
                                }
                            }

                        }
                        qMatrix.Swu -= reducingSum;
                        //uthv
                        DbDelta += reducingSum;

                        #region"Add to modified string Dictionary for output database"
                        string itemkey = itemMax.ToString() + "-" + itemsetIndex.ToString();
                        string itemvalue = ((int)Math.Ceiling((qMatrix.MatrixItemUtility[row][itemsetIndex] / externalData[itemMax]))).ToString();
                        if (modifyDic.ContainsKey(qMatrix.SeqID))
                        {
                            Dictionary<string, string> value = modifyDic[qMatrix.SeqID];
                            if (value.ContainsKey(itemkey))
                                value[itemkey] = itemvalue;
                            else
                                value.Add(itemkey, itemvalue);
                            modifyDic[qMatrix.SeqID] = value;
                        }
                        else
                        {
                            Dictionary<string, string> value = new Dictionary<string, string>
                            {
                                { itemkey, itemvalue }
                            };
                            modifyDic.Add(qMatrix.SeqID, value);
                        }
                        #endregion

                        if (diff < 0)
                            break;
                    }
                }
            }
            if (diff > 0)
            {
                break;
            }
        }
        HidingTotalTime += (DateTime.Now - start).TotalMilliseconds;

        writeModifiedData(modifyDic, uid);
    }
    public bool WillWriteModifiedData { get; set; } = true;
    public bool FirstWriteData = true;

    public void writeModifiedData(Dictionary<int, Dictionary<string, string>> modifyDic, string uid)
    {
        //dont need to
        if (!WillWriteModifiedData)
            return;

        string path = Path.GetDirectoryName(Application.ExecutablePath);
        string sanitizeFile = path + $"\\{uid}sanitized_ouput.txt";
        string tmpFile = path + "\\tmpFile.txt";

        //Open Modified Data to modify
        string output = $".//{uid}sanitized_ouput.txt";
        StreamWriter writer = new System.IO.StreamWriter(output);

        string output2 = ".//sanitized_ouput_detail.txt";
        StreamWriter writer2 = new System.IO.StreamWriter(output2);



        string thisLine;
        System.IO.StreamReader myInput;
        if (FirstWriteData == true)
        {
            myInput = new System.IO.StreamReader(new System.IO.FileStream(this.input, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite));
            FirstWriteData = false;
        }
        else
            myInput = new System.IO.StreamReader(new System.IO.FileStream(tmpFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite));
        int line = -1;
        while ((thisLine = myInput.ReadLine()) != null)
        {
            string s = "";
            // if the line is a comment, is  empty or is a kind of metadata, skip it
            if (thisLine.Length == 0 || thisLine[0] == '#' || thisLine[0] == '%' || thisLine[0] == '@')
            {
                continue;
            }
            line++;
            if (modifyDic.ContainsKey(line))
            {

                string s2;
                Dictionary<string, string> valueOfItem = modifyDic[line];
                for (int i = 0; i < valueOfItem.Count; i++)
                {
                    s2 = "Sequence " + line.ToString() + ":";
                    int CountOne = 0;
                    string key = valueOfItem.ElementAt(i).Key;
                    int positionColons = key.IndexOf('-');
                    int item = int.Parse(key.Substring(0, positionColons));
                    int itemset = int.Parse(key.Substring(positionColons + 1, key.Length - positionColons - 1));
                    string quantity = valueOfItem.ElementAt(i).Value;

                    string[] tokens;
                    if (i == 0)
                        tokens = thisLine.Split(" ", true);
                    else
                    {
                        tokens = s.Split(" ", true);
                        s = "";
                    }
                    float sequenceUtility = 0;

                    //Check if item just appears 1 time in this sequence
                    int count = 0;
                    for (int j = 0; j < tokens.Length - 3; j++)
                    {
                        string currentToken = tokens[j];
                        // if the current token is not -1 
                        if (currentToken.Length != 0 && currentToken[0] != '-')
                        {
                            int positionLeftBracketString = currentToken.IndexOf('[');
                            string itemString = currentToken.Substring(0, positionLeftBracketString);
                            int itemInt = int.Parse(itemString);
                            if (itemInt == item)
                                count++;
                        }
                    }

                    for (int j = 0; j < tokens.Length - 3; j++)
                    {
                        string currentToken = tokens[j];
                        // if the current token is not -1 
                        if (currentToken.Length != 0 && currentToken[0] != '-')
                        {
                            int positionLeftBracketString = currentToken.IndexOf('[');
                            int positionRightBracketString = currentToken.IndexOf(']');
                            string itemString = currentToken.Substring(0, positionLeftBracketString);
                            s += itemString + "[";
                            int itemInt = int.Parse(itemString);
                            string internalUtility;
                            if (itemInt == item && count == 1)
                            {
                                internalUtility = quantity;
                                sequenceUtility += int.Parse(internalUtility) * externalData[itemInt];
                            }
                            else
                            {
                                if (itemInt == item && itemset == CountOne)
                                {
                                    internalUtility = quantity;
                                    sequenceUtility += int.Parse(internalUtility) * externalData[itemInt];
                                }
                                else
                                {
                                    internalUtility = currentToken.Substring(positionLeftBracketString + 1, positionRightBracketString - (positionLeftBracketString + 1));
                                    sequenceUtility += int.Parse(internalUtility) * externalData[itemInt];
                                }
                            }
                            s += internalUtility + "] ";
                        }
                        else
                        {
                            if (currentToken.Length == 0)
                                continue;
                            if (currentToken[0] == '-')
                            {
                                s += "-1 ";
                                CountOne++;
                            }
                        }
                    }
                    s += " -2  SUtility:" + sequenceUtility;
                    s2 += s;
                    writer2.WriteLine(s2);
                }
                writer.WriteLine(s);
            }
            else
                writer.WriteLine(thisLine);
        }
        myInput.Close();
        writer.Close();
        writer2.Close();

        File.Copy(sanitizeFile, tmpFile, true);
    }

    bool checkFinalPeriodic(IList<int> seqIDList, float minPer, float maxPer, float minAvg, float maxAvg)
    {
        int preSeqID = seqIDList[0];
        int min = int.MaxValue, max = 0;
        for (int i = 1; i < seqIDList.Count; i++)
        {
            int seqID = seqIDList[i];
            if (seqID - preSeqID < minPer || seqID - preSeqID > maxPer)
            {
                return false;
            }

            var v = seqID - preSeqID;
            if (v < min)
                min = v;
            if (v > max)
                max = v;


            preSeqID = seqID;
        }
        if (preSeqID < sequenceCount)
        {
            if (sequenceCount - preSeqID < minPer || sequenceCount - preSeqID > maxPer)
            {
                return false;
            }

            var v = sequenceCount - preSeqID;
            if (v < min)
                min = v;
            if (v > max)
                max = v;
        }
        float peravg = 0;
        peravg = (float)sequenceCount / (seqIDList.Count + 1);
        if (peravg < minAvg || peravg > maxAvg)
            return false;

        //Console.WriteLine($"min:{min} max:{max}");
        return true;
    }
    bool checkPeriodicByIDList(IList<int> seqIDList, float maxPer)
    {

        int preSeqID = seqIDList[0];
        for (int i = 1; i < seqIDList.Count; i++)
        {
            int seqID = seqIDList[i];
            if (seqID - preSeqID > maxPer)
            {
                return false;
            }
            preSeqID = seqID;
        }
        if (preSeqID < sequenceCount)
        {
            if (sequenceCount - preSeqID > maxPer)
            {
                return false;
            }
        }
        return true;
    }
}