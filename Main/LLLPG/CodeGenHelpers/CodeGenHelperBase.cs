using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Loyc.Syntax;
using Loyc.Collections;
using Loyc.Utilities;
using S = Loyc.Syntax.CodeSymbols;

namespace Loyc.LLParserGenerator
{
	/// <summary>Suggested base class for custom code generators. Each derived 
	/// class is typically designed for a different kind of token.</summary>
	/// <remarks>
	/// LLPG comes with two derived classes, <see cref="IntStreamCodeGenHelper"/> 
	/// for parsing input streams of characters or integers, and 
	/// <see cref="GeneralCodeGenHelper"/> for parsing other streams. This class 
	/// contains common code used by both, for example:
	/// - default code snippets such as <c>LA0</c> and <c>LA(n)</c>, the default 
	///   error branch, and switch statements;
	/// - the decision function ShouldGenerateSwitch(); and
	/// - alias handling (alias "foo" = bar); note that the derived class's 
	///   NodeToPred() method is responsible for using _definedAliases.
	/// </remarks>
	public abstract class CodeGenHelperBase : IPGCodeGenHelper
	{
		protected static readonly Symbol _Skip = GSymbol.Get("Skip");
		protected static readonly Symbol _MatchAny = GSymbol.Get("MatchAny");
		protected static readonly Symbol _Match = GSymbol.Get("Match");
		protected static readonly Symbol _MatchExcept = GSymbol.Get("MatchExcept");
		protected static readonly Symbol _MatchRange = GSymbol.Get("MatchRange");
		protected static readonly Symbol _MatchExceptRange = GSymbol.Get("MatchExceptRange");
		protected static readonly Symbol _TryMatch = GSymbol.Get("TryMatch");
		protected static readonly Symbol _TryMatchExcept = GSymbol.Get("TryMatchExcept");
		protected static readonly Symbol _TryMatchRange = GSymbol.Get("TryMatchRange");
		protected static readonly Symbol _TryMatchExceptRange = GSymbol.Get("TryMatchExceptRange");
		protected static readonly Symbol _LA = GSymbol.Get("LA");
		protected static readonly Symbol _LA0 = GSymbol.Get("LA0");
		protected static readonly Symbol _Check = GSymbol.Get("Check");
		protected static readonly Symbol _Error = GSymbol.Get("Error");
		protected static readonly Symbol _underscore = GSymbol.Get("_");
		protected static readonly Symbol _alias = GSymbol.Get("alias");

		protected int _setNameCounter = 0;
		protected LNodeFactory F;
		protected RWList<LNode> _classBody;
		protected Rule _currentRule;
		Dictionary<IPGTerminalSet, Symbol> _setDeclNames;

		/// <summary>Specifies an object or class on which LLLPG APIs such as 
		/// Match() and LA() should be called.</summary>
		public LNode InputSource { get; set; }
		/// <summary>Specifies a class or namespace to use when calling static
		/// functions. There is only one currently: NewSet(), which applies only
		/// to .</summary>
		public LNode InputClass { get; set; }
		/// <summary>The type returned from Match() methods.</summary>
		public LNode TerminalType { get; set; }
		
		protected Dictionary<LNode, LNode> _definedAliases = new Dictionary<LNode, LNode>();
		protected LNode ResolveAlias(LNode expr)
		{
			LNode replacement;
			if (_definedAliases.TryGetValue(expr, out replacement))
				return replacement;
			return expr;
		}

		public virtual LNode VisitInput(LNode stmt, IMessageSink sink)
		{
			LNode aliasSet;
			if ((stmt.Calls(_alias, 1) || stmt.CallsMin(S.Alias, 1)) &&
				(aliasSet = stmt.Args[0]).Calls(S.Assign, 2))
			{
				IEnumerable<KeyValuePair<LNode, LNode>> q; 
				LNode alias = aliasSet.Args[0], replacement = aliasSet.Args[1], old;
				if (_definedAliases.TryGetValue(alias, out old)) {
					if (stmt.AttrNamed(S.Partial) == null || !old.Equals(replacement))
						sink.Write(Severity.Warning, alias, "Redefinition of alias '{0}'", alias);
				} else if ((q = _definedAliases.Where(pair => replacement.Equals(pair.Value))).Any())
					sink.Write(Severity.Warning, replacement, "Aliases '{0}' and '{1}' have the same replacement value", q.First().Key, alias);
				_definedAliases[alias] = replacement;
				return LNode.Call(S.Splice, RVList<LNode>.Empty); // erase alias from output
			}
			return null;
		}

		public abstract IPGTerminalSet EmptySet { get; }
		public abstract Pred CodeToPred(LNode expr, ref string errorMsg);
		public virtual IPGTerminalSet Optimize(IPGTerminalSet set, IPGTerminalSet dontcare) { return set.Subtract(dontcare); }
		public virtual char? ExampleChar(IPGTerminalSet set) { return null; }
		public abstract string Example(IPGTerminalSet set);

		public virtual void Begin(RWList<LNode> classBody, ISourceFile sourceFile)
		{
			_classBody = classBody;
			F = new LNodeFactory(sourceFile);
			_setDeclNames = new Dictionary<IPGTerminalSet, Symbol>();
		}
		public virtual void BeginRule(Rule rule)
		{
			_currentRule = rule;
			_setNameCounter = 0;
		}
		public virtual void Done()
		{
			_classBody = null;
			F = null;
			_setDeclNames = null;
			_currentRule = null;
		}

		public virtual LNode GenerateTest(IPGTerminalSet set, LNode laVar)
		{
			LNode test = GenerateTest(set, laVar, null);
			if (test == null)
			{
				var setName = GenerateSetDecl(set);
				test = GenerateTest(set, laVar, setName);
			}
			return test;
		}

		/// <summary>Generates code to test whether a terminal is in the set.</summary>
		/// <param name="subject">Represents the variable to be tested.</param>
		/// <param name="setName">Names an external set variable to use for the test.</param>
		/// <returns>A test expression such as <c>(la0 >= '0' &amp;&amp; '9' >= la0)</c>, or 
		/// null if an external setName is needed and was not provided.</returns>
		/// <remarks>
		/// At first, <see cref="LLParserGenerator"/> calls this method with 
		/// <c>setName == null</c>. If it returns null, it calls the method a
		/// second time, giving the name of an external variable in which the
		/// set is held (see <see cref="GenerateSetDecl(IPGTerminalSet)"/>).
		/// <para/>
		/// For example, if the subject is @la0, the test for a simple set
		/// like [a-z?] might be something like <c>(la0 >= 'a' &amp;&amp; 'z' >= la0)
		/// || la0 == '?'</c>. When the setName is <c>foo</c>, the test might be 
		/// <c>foo.Contains(la0)</c> instead.
		/// </remarks>
		protected abstract LNode GenerateTest(IPGTerminalSet set, LNode subject, Symbol setName);

		protected virtual Symbol GenerateSetName(Rule currentRule)
		{
			return GSymbol.Get(string.Format("{0}_set{1}", currentRule.Name.Name, _setNameCounter++));
		}

		protected virtual Symbol GenerateSetDecl(IPGTerminalSet set)
		{
			Symbol setName;
			if (_setDeclNames.TryGetValue(set, out setName))
				return setName;

			setName = GenerateSetName(_currentRule);
			_classBody.Add(GenerateSetDecl(set, setName));

			return _setDeclNames[set] = setName;
		}

		/// <summary>Generates a declaration for a variable that holds the set.</summary>
		/// <remarks>
		/// For example, if setName is foo, a set such as [aeiouy] 
		/// might use an external declaration such as 
		/// <code>HashSet&lt;int> foo = NewSet('a', 'e', 'i', 'o', 'u', 'y');</code>
		/// </remarks>
		protected abstract LNode GenerateSetDecl(IPGTerminalSet set, Symbol setName);

		/// <summary>Returns <c>(Skip())</c>, or <c>(MatchAny())</c> if the result 
		/// is to be saved.</summary>
		public virtual LNode GenerateSkip(bool savingResult) // match anything
		{
			if (savingResult && !_currentRule.IsRecognizer)
				return ApiCall(_MatchAny);
			else
				return ApiCall(_Skip);
		}

		/// <summary>Generate code to check an and-predicate during or after prediction, 
		/// e.g. &amp;!{foo} becomes !(foo) during prediction and Check(!(foo)); afterward.</summary>
		/// <param name="andPred">Predicate for which an expression has already been generated</param>
		/// <param name="code">The expression to be checked</param>
		/// <param name="li">Current lookahead amount. -1 means "prediction is 
		/// complete, generate a Check() statement".</param>
		/// <remarks>LLLPG substitutes $LI and $LA before it calls this method.</remarks>
		public virtual LNode GenerateAndPredCheck(AndPred andPred, LNode code, int li)
		{
			if (_currentRule.IsRecognizer && li <= -1)
			{
				if (!andPred.Not)
					code = F.Call(S.Not, code);
				return F.Call(S.If, code, F.Call(S.Return, F.@false));
			}

			if (andPred.Not)
				code = F.Call(S.Not, code);
			if (li > -1)
				return code;
			else
			{
				string asString = (andPred.Pred is LNode
					? ((LNode)andPred.Pred).Print(NodeStyle.Expression)
					: andPred.Pred.ToString());
				if (andPred.Not)
					asString = "!(" + asString + ")";
				return ApiCall(_Check, code, F.Literal(asString));
			}
		}

		public virtual LNode GenerateMatch(IPGTerminalSet set, bool savingResult, bool recognizerMode)
		{
			LNode call = GenerateMatchExpr(set, savingResult, recognizerMode);
			if (recognizerMode)
				return F.Call(S.If, F.Call(S.Not, call), F.Call(S.Return, F.@false));
			else
				return call;
		}
		/// <summary>Generate code to match a set, e.g. 
		/// <c>@{ MatchRange('a', 'z');</c> or <c>@{ MatchExcept('\n', '\r'); }</c>.
		/// If the set is too complex, a declaration for it is created in classBody.</summary>
		public abstract LNode GenerateMatchExpr(IPGTerminalSet set, bool savingResult, bool recognizerMode);

		/// <summary>Generates code to read LA(k).</summary>
		/// <returns>Default implementation returns LA0 for k==0, LA(k) otherwise.</returns>
		public virtual LNode LA(int k)
		{
			return k == 0 ? ApiCall(_LA0, null, false) 
			              : ApiCall(_LA,  F.Literal(k));
		}

		/// <summary>Generates code for the default error branch of prediction
		/// (called when there is no explicit error branch).</summary>
		/// <param name="covered">The permitted token set, which the input did not match. 
		/// NOTE: if the input matched but there were and-predicates that did not match,
		/// this parameter will be null (e.g. the input is 'b' in <c>(&amp;{x} 'a' | &amp;{y} 'b')</c>,
		/// but y is false.</param>
		/// <param name="laIndex">Location of unexpected input, relative to current position.</param>
		public virtual LNode ErrorBranch(IPGTerminalSet covered, int laIndex)
		{
			string coveredS = covered.ToString();
			if (coveredS.Length > 45)
				coveredS = coveredS.Substring(0, 40) + "...";
			return ApiCall(_Error, F.Literal(laIndex),
				F.Literal(string.Format("In rule '{0}', expected one of: {1}", _currentRule.Name.Name, coveredS)));
		}

		/// <summary>Returns the data type of LA(k)</summary>
		/// <returns>Default implementation returns @(int).</returns>
		public abstract LNode LAType();

		/// <summary>Used to help decide whether a "switch" or an if-else chain 
		/// will be used for prediction. This is the starting cost of a switch 
		/// (the starting cost of an if-else chain is set to zero).</summary>
		protected virtual int BaseCostForSwitch { get { return 8; } }
		/// <summary>Used to help decide whether a "switch" or an if statement
		/// will be used to handle a prediction tree, and if so which branches.
		/// This method should calculate the "cost of switch" (which generally 
		/// represents a code size penalty, as there is a separate case for 
		/// every element of the set) and the "cost of if" (which generally 
		/// represents a speed penalty) and return the difference (so that 
		/// positive numbers favor "switch" and negative numbers favor "if".)</summary>
		/// <remarks>If the set is inverted, return a something like -1000000 
		/// to ensure 'switch' is not used for that set.</remarks>
		protected virtual int GetRelativeCostForSwitch(IPGTerminalSet set) { return -1000000; }
		/// <summary>Gets the literals or symbols to use for switch cases of
		/// a set (just the values, not including the case labels.)</summary>
		protected virtual IEnumerable<LNode> GetCases(IPGTerminalSet set) { throw new NotImplementedException(); }

		/// <summary>Decides whether to use a switch() and for which cases, using
		/// <see cref="BaseCostForSwitch"/> and <see cref="GetRelativeCostForSwitch"/>.</summary>
		public virtual bool ShouldGenerateSwitch(IPGTerminalSet[] sets, MSet<int> casesToInclude, bool hasErrorBranch)
		{
			// Compute scores
			IPGTerminalSet covered = EmptySet;
			int[] score = new int[sets.Length - 1]; // no error branch? then last set must be default
			for (int i = 0; i < score.Length; i++)
			{
				Debug.Assert(sets[i].Subtract(covered).Equals(sets[i]));
				score[i] = GetRelativeCostForSwitch(sets[i]);
			}

			// Consider highest scores first to figure out whether switch is 
			// justified, and which branches should be expressed with "case"s.
			bool should = false;
			int switchScore = -BaseCostForSwitch;
			for (; ; )
			{
				int maxIndex = score.IndexOfMax(), maxScore = score[maxIndex];
				switchScore += maxScore;
				if (switchScore > 0)
					should = true;
				else if (maxScore < 0)
					break;
				casesToInclude.Add(maxIndex);
				score[maxIndex] = -1000000;
			}
			return should;
		}

		public virtual LNode GenerateSwitch(IPGTerminalSet[] branchSets, MSet<int> casesToInclude, LNode[] branchCode, LNode defaultBranch, LNode laVar)
		{
			Debug.Assert(branchSets.Length == branchCode.Length);

			RWList<LNode> stmts = new RWList<LNode>();
			for (int i = 0; i < branchSets.Length; i++)
			{
				if (casesToInclude.Contains(i))
				{
					foreach (LNode value in GetCases(branchSets[i]))
					{
						stmts.Add(F.Call(S.Case, value));
						if (stmts.Count > 65535) // sanity check
							throw new InvalidOperationException("switch is too large to generate");
					}
					AddSwitchHandler(branchCode[i], stmts);
				}
			}

			if (!defaultBranch.IsIdNamed(S.Missing))
			{
				stmts.Add(F.Call(S.Label, F.Id(S.Default)));
				AddSwitchHandler(defaultBranch, stmts);
			}

			return F.Call(S.Switch, (LNode)laVar, F.Braces(stmts.ToRVList()));
		}
		private void AddSwitchHandler(LNode branch, RWList<LNode> stmts)
		{
			stmts.SpliceAdd(branch, S.Splice);
			if (EndMayBeReachable(branch))
				stmts.Add(F.Call(S.Break));
		}

		// Decides whether to add a "break" at the end of a switch case.
		internal protected static bool EndMayBeReachable(LNode stmt)
		{
			// The goal of this code is to avoid the dreaded compiler warning 
			// "Unreachable code detected". We're conservative, to avoid a compiler 
			// error about a missing "break". This is just a heuristic since we 
			// don't have access to proper reachability analysis.
			if (stmt.CallsMin(S.Braces, 1))
				return EndMayBeReachable(stmt.Args.Last);
			if (!stmt.HasSpecialName)
				return true;

			if (stmt.Calls(S.Goto, 1))
				return false;
			else if (stmt.Calls(S.Continue) || stmt.Calls(S.Break))
				return false;
			else if (stmt.Calls(S.Return))
				return false;
			else if (stmt.Calls(S.GotoCase, 1))
				return false;

			LNode body;
			if (stmt.Calls(S.If, 2))
				return true;
			else if (stmt.Calls(S.If, 3))
			{
				return EndMayBeReachable(stmt.Args[1])
					|| EndMayBeReachable(stmt.Args[2]);
			}
			else if (stmt.CallsMin(S.Switch, 2) && (body = stmt.Args[1]).CallsMin(S.Braces, 2))
			{
				// for a switch statement, assume it exits normally if a break 
				// statement is the last statement of any of the cases, or if
				// there is no "default" case.
				bool beforeCase = true;
				bool hasDefaultCase = false;
				for (int i = body.ArgCount - 1; i > 0; i--)
				{
					var substmt = body.Args[i];
					if (beforeCase && substmt.Calls(S.Break))
						return true;
					if (substmt.Calls(S.Label, 1) && substmt.Args[0].IsIdNamed(S.Default))
						hasDefaultCase = beforeCase = true;
					else
						beforeCase = substmt.Calls(S.Case);
				}
				return hasDefaultCase == false;
			}
			else if (stmt.Calls(S.For) || stmt.Calls(S.While) || stmt.Calls(S.DoWhile))
			{
				return true;
			}
			else if (stmt.CallsMin(S.Try, 1))
			{
				return EndMayBeReachable(stmt.Args[0]);
			}
			else if (stmt.ArgCount >= 1)
			{
				Debug.Assert(stmt.HasSpecialName);
				return EndMayBeReachable(stmt.Args.Last);
			}
			return true;
		}

		public virtual LNode CreateRuleMethod(Rule rule, RVList<LNode> methodBody)
		{
			return rule.CreateMethod(methodBody);
		}
		public LNode CreateTryWrapperForRecognizer(Rule rule)
		{
			return rule.CreateTryWrapperForRecognizer();
		}

		public virtual LNode CallRule(RuleRef rref, bool recognizerMode)
		{
			Rule target = rref.Rule;
			var @params = rref.Params;
			if (recognizerMode)
			{
				target = target.MakeRecognizerVersion();

				// Allow recognizers to take fewer arguments than the normal rule 
				// by truncating argument(s) at the call site.
				int maxArgCount = target.Basis.CallsMin(S.Fn, 3) ? target.Basis.Args[2].ArgCount : 0;
				if (@params.Count > maxArgCount)
					@params = @params.First(maxArgCount);
			}
			LNode call = F.Call(target.Name, @params);
			if (recognizerMode)
				return F.Call(S.If, F.Call(S.Not, call), F.Call(S.Return, F.@false));
			else
				return rref.AutoSaveResult(call);
		}

		public virtual LNode CallTryRecognizer(RuleRef rref, int lookahead)
		{
			Rule target = rref.Rule;
			target = target.MakeRecognizerVersion();
			LNode name = target.TryWrapperName;
			var @params = rref.Params;
			return F.Call(name, @params.Insert(0, F.Literal(lookahead)));
		}

		/// <summary>Returns an LNode representing a call to the specified LLLPG API.
		/// For example, if the user used a "inputSource=input" option, then 
		/// <c>ApiCall(_Match, F.Literal('7'))</c> would generate a node that 
		/// represents <c>input.Match('7')</c>.</summary>
		protected virtual LNode ApiCall(Symbol apiName, params LNode[] args)
		{
			return ApiCall(apiName, (IEnumerable<LNode>)args);
		}
		/// <summary>Returns an LNode representing a call to the specified LLLPG API.
		/// For example, if the user used a "inputSource=input" option, then 
		/// <c>ApiCall(_Match, F.Literal('7'))</c> would generate a node that 
		/// represents <c>input.Match('7')</c>.</summary>
		/// <param name="args">Parameters to the API call, or null to access a 
		/// property or field.</param>
		protected virtual LNode ApiCall(Symbol apiName, IEnumerable<LNode> args, bool isStatic = false)
		{
			LNode inputSource = isStatic ? InputClass : InputSource;
			LNode result;
			if (inputSource != null) {
				result = F.Dot(inputSource, F.Id(apiName));
				if (args != null)
					result = F.Call(result, args);
			} else {
				if (args == null)
					result = F.Id(apiName);
				else
					result = F.Call(apiName, args);
			}
			return result;
		}
	}
}
