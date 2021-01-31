using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public partial class FrmMain : Form
{
    public FrmMain()
    {
        InitializeComponent();
    }
    private void btnOpen_Click(object sender, EventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.InitialDirectory = @"~\Debug";
        openFileDialog.Title = "Browse Text Files";

        openFileDialog.CheckFileExists = true;
        openFileDialog.CheckPathExists = true;
        openFileDialog.Multiselect = true;

        openFileDialog.DefaultExt = "txt";
        openFileDialog.Filter = "Data (.txt)|*.txt|All files (*.*)|*.*";
        openFileDialog.FilterIndex = 1;

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            txtInputFile.Text = openFileDialog.FileName;
        }
        else
            return;
    }
    public void addDataToTreeViewForMining(PUSOM pusom)
    {
        TreeNode node = new TreeNode();
        node = listTreeHUSP.Nodes.Add("PUSOM algorithm in database " + txtInputFile.Text + " with minUntil = " + txtminUntil.Text);
        string file = txtInputFile.Text.Substring(txtInputFile.Text.LastIndexOf("\\") + 1, txtInputFile.Text.LastIndexOf(".") - txtInputFile.Text.LastIndexOf("\\") - 1);
        switch (file)
        {
            case "demo":
                {
                    node.Nodes.Add("Size: 1 KB, #Sequences: 5, #Distinct items: 6, Avg seq length: 7");
                    break;
                }
            case "kosarak10k":
                {
                    node.Nodes.Add("Size: 0.98 MB, #Sequences: 10000, #Distinct items: 10094, Avg seq length: 8.14");
                    break;
                }
            case "sign":
                {
                    node.Nodes.Add("Size: 375 KB, #Sequences: 800, #Distinct items: 267, Avg seq length: 51.99");
                    break;
                }
            case "fifa":
                {
                    node.Nodes.Add("Size: 7.59 MB, #Sequences: 20450, #Distinct items: 2990, Avg seq length: 34.74");
                    break;
                }
            case "bible":
                {
                    node.Nodes.Add("Size: 8.56 MB, #Sequences: 36369, #Distinct items: 13905, Avg seq length: 21.64");
                    break;
                }
            case "bmswebview1":
                {
                    node.Nodes.Add("Size: 2.80 MB, #Sequences: 59601, #Distinct items: 497, Avg seq length: 2.51");
                    break;
                }
            case "bmswebview2":
                {
                    node.Nodes.Add("Size: 3.45 MB, #Sequences: 77512, #Distinct items: 3340, Avg seq length: 4.62");
                    break;
                }
            case "kosarak990k":
                {
                    node.Nodes.Add("Size: 57.2 MB, #Sequences: 990002, #Distinct items: 41270, Avg seq length: 8.14");
                    break;
                }
        }
        node.Nodes.Add("Total time: ~" + (pusom.endTimestamp - pusom.startTimestamp) + " ms");
        node.Nodes.Add("Memory Usage: ~" + (pusom.currentProc.PrivateMemorySize64 / 1024) / 1024 + "Mb");
        if (txtMaxLength.Text == "")
            node.Nodes.Add("Max length: All");
        else
            node.Nodes.Add("Max length: " + txtMaxLength.Text);
        TreeNode childNode = new TreeNode("Periodic High utility sequential patterns: " + pusom.patternCount);
        node.Nodes.Add(childNode);
        for (int i = 0; i < pusom.highUtilitySet.Count(); i++)
        {
            int[] items = pusom.highUtilitySet.ElementAt(i).Key;
            float utility = pusom.highUtilitySet.ElementAt(i).Value;
            StringBuilder buffer = new StringBuilder();
            buffer.Append('<');
            buffer.Append('(');
            for (int j = 0; j < items.Length; j++)
            {
                if (items[j] == -1)
                {
                    buffer.Append(")(");
                }
                else
                {
                    buffer.Append(items[j]);
                }
            }
            buffer.Append(")>:");
            buffer.Append(utility);
            childNode.Nodes.Add(buffer.ToString());
        }
        listTreeHUSP.ExpandAll();
    }

    private void btnExit_Click(object sender, EventArgs e)
    {
        Application.Exit();
    }

    private void btnShowData_Click(object sender, EventArgs e)
    {
        System.Diagnostics.Process.Start(txtInputFile.Text);
    }
    private void btnPUSH_Click(object sender, EventArgs e)
    {
        var pusom = new PUSOM();
        float minPer = float.Parse(numMinPdc.Value.ToString());
        float maxPer = float.Parse(numMaxPdc.Value.ToString());
        float minAvg = float.Parse(numMinAvg.Value.ToString());
        float maxAvg = float.Parse(numMaxAvg.Value.ToString());
        listTreeHUSP.Nodes.Clear();
        string external = txtInputFile.Text.Substring(0, txtInputFile.Text.LastIndexOf("."));
        external += "_ExternalUtility.txt";
        // the path for saving the patterns found
        string output = ".//output.txt";
        // run the algorithm
        if (txtminUntil.Text == "")
            MessageBox.Show("Input minimum utility");
        else
        {
            IList<QMatrixHUSSpan> database = pusom.loadDataWithInternalExternal(external, txtInputFile.Text, int.Parse(txtminUntil.Text));
            if (txtMaxLength.Text != "")
            {
                pusom.setMaxPatternLength(int.Parse(txtMaxLength.Text));
            }
            pusom.runAlgorithm(database, output, minPer, maxPer, minAvg, maxAvg);
            //MessageBox.Show("Finish");
            addDataToTreeViewForMining(pusom);
        }
    }

    private void btnOutput_Click(object sender, EventArgs e)
    {
        System.Diagnostics.Process.Start(txtOutputFile.Text);
    }

    private void btnHiding_Click(object sender, EventArgs e)
    {
        var phusHider = new PHUSHiding();
        float minPdc = float.Parse(numMinPdc.Value.ToString());
        float maxPdc = float.Parse(numMaxPdc.Value.ToString());
        float minAvg = float.Parse(numMinAvg.Value.ToString());
        float maxAvg = float.Parse(numMaxAvg.Value.ToString());
        listTreeHUSP.Nodes.Clear();
        string external = txtInputFile.Text.Substring(0, txtInputFile.Text.LastIndexOf("."));
        external += "_ExternalUtility.txt";
        // the path for saving the patterns found
        string output = ".//output.txt";
        // run the algorithm
        if (txtminUntil.Text == "")
            MessageBox.Show("Input minimum utility");
        else
        {
            Dictionary<int, QMatrixHUSSpan> database = phusHider.loadDataWithInternalExternal(external, txtInputFile.Text, int.Parse(txtminUntil.Text));
            if (txtMaxLength.Text != "")
            {
                phusHider.setMaxPatternLength(int.Parse(txtMaxLength.Text));
            }
            var uid = DateTime.Now.ToString("ddMMyyyy_hhmmss");

            phusHider.runAlgorithm(database, output, minPdc, maxPdc, minAvg, maxAvg, uid);
            addDataToTreeViewForHiding(phusHider);
        }
    }
    public void addDataToTreeViewForHiding(PHUSHiding phusHider)
    {
        TreeNode node = new TreeNode();
        node = listTreeHUSP.Nodes.Add("PUSOM algorithm in database " + txtInputFile.Text + " with minUntil = " + txtminUntil.Text);
        string file = txtInputFile.Text.Substring(txtInputFile.Text.LastIndexOf("\\") + 1, txtInputFile.Text.LastIndexOf(".") - txtInputFile.Text.LastIndexOf("\\") - 1);
        switch (file)
        {
            case "demo":
                {
                    node.Nodes.Add("Size: 1 KB, #Sequences: 5, #Distinct items: 6, Avg seq length: 7");
                    break;
                }
            case "kosarak10k":
                {
                    node.Nodes.Add("Size: 0.98 MB, #Sequences: 10000, #Distinct items: 10094, Avg seq length: 8.14");
                    break;
                }
            case "sign":
                {
                    node.Nodes.Add("Size: 375 KB, #Sequences: 800, #Distinct items: 267, Avg seq length: 51.99");
                    break;
                }
            case "fifa":
                {
                    node.Nodes.Add("Size: 7.59 MB, #Sequences: 20450, #Distinct items: 2990, Avg seq length: 34.74");
                    break;
                }
            case "bible":
                {
                    node.Nodes.Add("Size: 8.56 MB, #Sequences: 36369, #Distinct items: 13905, Avg seq length: 21.64");
                    break;
                }
            case "bmswebview1":
                {
                    node.Nodes.Add("Size: 2.80 MB, #Sequences: 59601, #Distinct items: 497, Avg seq length: 2.51");
                    break;
                }
            case "bmswebview2":
                {
                    node.Nodes.Add("Size: 3.45 MB, #Sequences: 77512, #Distinct items: 3340, Avg seq length: 4.62");
                    break;
                }
            case "kosarak990k":
                {
                    node.Nodes.Add("Size: 57.2 MB, #Sequences: 990002, #Distinct items: 41270, Avg seq length: 8.14");
                    break;
                }
        }
        node.Nodes.Add("Total time: ~" + (phusHider.endTimestamp - phusHider.startTimestamp) + " ms");
        node.Nodes.Add("Memory Usage: ~" + (phusHider.currentProc.PrivateMemorySize64 / 1024) / 1024 + "Mb");
        if (txtMaxLength.Text == "")
            node.Nodes.Add("Max length: All");
        else
            node.Nodes.Add("Max length: " + txtMaxLength.Text);
        TreeNode childNode = new TreeNode("Periodic High utility sequential patterns: " + phusHider.PHUSPCount);
        node.Nodes.Add(childNode);
        for (int i = 0; i < phusHider.highUtilitySet.Count(); i++)
        {
            int[] items = phusHider.highUtilitySet.ElementAt(i).Key;
            float utility = phusHider.highUtilitySet.ElementAt(i).Value;
            StringBuilder buffer = new StringBuilder();
            buffer.Append('<');
            buffer.Append('(');
            for (int j = 0; j < items.Length; j++)
            {
                if (items[j] == -1)
                {
                    buffer.Append(")(");
                }
                else
                {
                    buffer.Append(items[j]);
                }
            }
            buffer.Append(")>:");
            buffer.Append(utility);
            childNode.Nodes.Add(buffer.ToString());
        }
        listTreeHUSP.ExpandAll();
    }

}
