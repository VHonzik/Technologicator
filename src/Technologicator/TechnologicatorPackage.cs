using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Text;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text;

namespace BISim.Technologicator
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidTechnologicatorPkgString)]
    public sealed class TechnologicatorPackage : Package
    {

        private DTE _dte;
        private IVsTextManager _textManager;

        private IVsEditorAdaptersFactoryService _adapters;


        private string _selectedTechnology;

        private string _chooseOrigText;
        private OleMenuCommand _chooseCommandItem;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public TechnologicatorPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
            _selectedTechnology = "";
            _chooseOrigText = "TC - Choose";
        }



        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                // Create the command for the menu item.

                CommandID iTTCommandID = new CommandID(GuidList.guidTechnologicatorCmdSet, (int)PkgCmdIDList.cmdidTechnologicatorOpenITT);
                MenuCommand itt = new MenuCommand(IssueTrackingWebCallback, iTTCommandID);
                mcs.AddCommand(itt);

                CommandID chooseCommandID = new CommandID(GuidList.guidTechnologicatorCmdSet, (int)PkgCmdIDList.cmdidTechnologicatorChoose);
                _chooseCommandItem = new OleMenuCommand(ChooseTechCallback, chooseCommandID);
                _chooseCommandItem.BeforeQueryStatus += new EventHandler(ChooseBeforeStatusQueryCallback);
                mcs.AddCommand(_chooseCommandItem);

                CommandID addCommandID = new CommandID(GuidList.guidTechnologicatorCmdSet, (int)PkgCmdIDList.cmdidTechnologicatorAdd);
                OleMenuCommand addItem = new OleMenuCommand(AddTechCallback, addCommandID);
                addItem.BeforeQueryStatus += new EventHandler(TechButtonsBeforeStatusQueryCallback);
                mcs.AddCommand(addItem);

                CommandID changeCommandID = new CommandID(GuidList.guidTechnologicatorCmdSet, (int)PkgCmdIDList.cmdidTechnologicatorChange);
                OleMenuCommand changeItem = new OleMenuCommand(ChangeTechCallback, changeCommandID);
                changeItem.BeforeQueryStatus += new EventHandler(TechButtonsBeforeStatusQueryCallback);
                mcs.AddCommand(changeItem);

                CommandID removeCommandID = new CommandID(GuidList.guidTechnologicatorCmdSet, (int)PkgCmdIDList.cmdidTechnologicatorRemove);
                OleMenuCommand removeItem = new OleMenuCommand(RemoveTechCallback, removeCommandID);
                removeItem.BeforeQueryStatus += new EventHandler(TechButtonsBeforeStatusQueryCallback);
                mcs.AddCommand(removeItem);
            }

            _dte = (DTE)GetService(typeof(DTE));
            _textManager = GetService(typeof(VsTextManagerClass)) as IVsTextManager;

            var componentModel = (IComponentModel)GetService(typeof(SComponentModel));
            _adapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();

        }
        #endregion

        private void TechButtonsBeforeStatusQueryCallback(object sender, EventArgs e)
        {
            var command = sender as OleMenuCommand;
            if (command != null)
            {
                command.Enabled = _selectedTechnology.Length > 0;                  
            }
        }


        private void ChooseBeforeStatusQueryCallback(object sender, EventArgs e)
        {
            var chooseCommand = sender as OleMenuCommand;
            if (chooseCommand != null)
            {

                if (_selectedTechnology.Length > 0)
                    chooseCommand.Text = _chooseOrigText + " - " + _selectedTechnology;
                else
                    chooseCommand.Text = _chooseOrigText;
            }
        }

        private void IssueTrackingWebCallback(object sender, EventArgs e)
        {
            EnvDTE.TextSelection ts = _dte.ActiveWindow.Selection as EnvDTE.TextSelection;

            if (String.IsNullOrEmpty(TechnologicatorDefines.IssueTrackingWebURL)
                || String.IsNullOrEmpty(TechnologicatorDefines.ConfigFile)
                || String.IsNullOrEmpty(TechnologicatorDefines.TaskRegex))
                return;

            SelectWord(ts);
                
            if (ts.Text.StartsWith("[") && ts.Text.EndsWith("]"))
            {
                OpenTaskInBrowser(ProcessBracketedTaskName(ts.Text));
            }
            else
            {
                string task = GetTaskNameFromTechnology(ts.Text);
                if (task != "")
                {
                    OpenTaskInBrowser(ProcessBracketedTaskName(task));
                }
            }
        }

        private void ChooseTechCallback(object sender, EventArgs e)
        {
            EnvDTE.TextSelection ts = _dte.ActiveWindow.Selection as EnvDTE.TextSelection;
            SelectWord(ts);
            _selectedTechnology = ts.Text;
        }

        private void AddTechCallback(object sender, EventArgs e)
        {
            IVsTextView textView;
            if (_textManager.GetActiveView(1, null, out textView) == 0)
            {
                TextSpan[] span = new TextSpan[1];
                int selStart, temp;
               
                textView.GetSelectionSpan(span);
                textView.GetNearestPosition(span[0].iStartLine, span[0].iStartIndex, out selStart, out temp);

                IVsTextLines lines;
                if (textView.GetBuffer(out lines) == 0)
                {
                
                    var linesTextBuffer = lines as IVsTextBuffer;

                    ITextBuffer buffer = _adapters.GetDataBuffer(linesTextBuffer);
                    ITextEdit edit = buffer.CreateEdit();
                    string text = Environment.NewLine+"#if " + _selectedTechnology + Environment.NewLine + Environment.NewLine +"#endif // " + _selectedTechnology.Substring(1) + Environment.NewLine;
                    edit.Insert(selStart, text);
                    edit.Apply();
                }               

            }
        }

        private void ChangeTechCallback(object sender, EventArgs e)
        {
            IVsTextView textView;
            if (_textManager.GetActiveView(1, null, out textView) == 0)
            {
                TextSpan[] span = new TextSpan[1];
                int selStart, selEnd, temp;

                textView.GetSelectionSpan(span);
                textView.GetNearestPosition(span[0].iStartLine, span[0].iStartIndex, out selStart, out temp);
                textView.GetNearestPosition(span[0].iEndLine, span[0].iEndIndex, out selEnd, out temp);

                IVsTextLines lines;
                if (textView.GetBuffer(out lines) == 0)
                {

                    var linesTextBuffer = lines as IVsTextBuffer;

                    ITextBuffer buffer = _adapters.GetDataBuffer(linesTextBuffer);

                    string currentCode = buffer.CurrentSnapshot.GetText(selStart, selEnd - selStart);

                    ITextEdit edit = buffer.CreateEdit();
                    edit.Delete(selStart, selEnd - selStart);

                    string text = "#if " + _selectedTechnology + Environment.NewLine +
                        currentCode + Environment.NewLine +
                        "#else" + Environment.NewLine +
                        currentCode + Environment.NewLine +
                        "#endif // " + _selectedTechnology.Substring(1) + Environment.NewLine;

                    edit.Insert(selStart, text);
                    edit.Apply();
                }              
            }
        }

        private void RemoveTechCallback(object sender, EventArgs e)
        {

            IVsTextView textView;
            if (_textManager.GetActiveView(1, null, out textView) == 0)
            {
                TextSpan[] span = new TextSpan[1];
                int selStart, selEnd, temp;

                textView.GetSelectionSpan(span);
                textView.GetNearestPosition(span[0].iStartLine, span[0].iStartIndex, out selStart, out temp);
                textView.GetNearestPosition(span[0].iEndLine, span[0].iEndIndex, out selEnd, out temp);

                IVsTextLines lines;
                if (textView.GetBuffer(out lines) == 0)
                {
                    var linesTextBuffer = lines as IVsTextBuffer;

                    ITextBuffer buffer = _adapters.GetDataBuffer(linesTextBuffer);

                    string currentCode = buffer.CurrentSnapshot.GetText(selStart, selEnd - selStart);

                    ITextEdit edit = buffer.CreateEdit();
                    edit.Delete(selStart, selEnd - selStart);

                    string text = "#if !" + _selectedTechnology + Environment.NewLine +
                        currentCode + Environment.NewLine +
                        "#endif // !" + _selectedTechnology.Substring(1) + Environment.NewLine;

                    edit.Insert(selStart, text);
                    edit.Apply();
                }
            }
        }

        private void SelectWord(EnvDTE.TextSelection ts)
        {
            if (!ts.IsEmpty)
                return;

            ts.WordLeft(true);
            ts.SwapAnchor();
            ts.WordRight(true);
        }

        private string GetTaskNameFromTechnology(string techName)
        {
            string configFile = System.IO.Path.GetDirectoryName(_dte.Solution.FullName) + TechnologicatorDefines.ConfigFile;
            _dte.ExecuteCommand("File.OpenFile", configFile);
            _dte.Find.MatchWholeWord = false;
            _dte.Find.Action = vsFindAction.vsFindActionFind;
            _dte.Find.Target = vsFindTarget.vsFindTargetCurrentDocument;
            _dte.Find.MatchCase = false;
            _dte.Find.Backwards = false;
            _dte.Find.MatchInHiddenText = true;
            _dte.Find.PatternSyntax = vsFindPatternSyntax.vsFindPatternSyntaxLiteral;
            _dte.Find.FindWhat = "#define " + techName;
            if (_dte.Find.Execute() != vsFindResult.vsFindResultFound)
                return "";
            EnvDTE.TextSelection ts = _dte.ActiveWindow.Selection as EnvDTE.TextSelection;
            ts.SelectLine();
            string defLine = ts.Text;
            string taskRegexPattern = TechnologicatorDefines.TaskRegex;
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(defLine, taskRegexPattern);

            if (m.Success)
            {
                return m.Value;
            }
            else
            {
                return "";
            }
        }


        private string ProcessBracketedTaskName(string bracketedName)
        {
            return bracketedName.Substring(1, bracketedName.Length - 2);
        }

        private void OpenTaskInBrowser(string taskName)
        {
            System.Diagnostics.Process.Start(TechnologicatorDefines.IssueTrackingWebURL + taskName);
        }

    }
}
