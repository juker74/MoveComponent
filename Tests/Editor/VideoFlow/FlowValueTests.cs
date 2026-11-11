using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using GameFrameX.NodeComposer;

namespace Game.VideoFlow.Tests
{
    public class FlowValueTests
    {
        static NodeDocument Node(string id, string type, params object[] properties)
        {
            var node = new NodeDocument { Id=id, TypeId=type };
            for (int i=0;i<properties.Length;i+=2) node.Properties[(string)properties[i]]=properties[i+1];
            return node;
        }
        static EdgeDocument Edge(string from,string output,string to,string input="enter",int order=0)
        { return new EdgeDocument { Id=from+output+to+input, SourceNodeId=from,SourcePortId=output,TargetNodeId=to,TargetPortId=input,Order=order }; }
        static string Json(NodeDefinitionRegistry registry,NodeDocument[] nodes,params EdgeDocument[] edges)
        {
            var result=new GraphJsonCodec(registry).Encode(new GraphDocument { Nodes=nodes.ToList(),Edges=edges.ToList() });
            Assert.That(result.Success,Is.True,result.Message);return result.Value;
        }
        sealed class UnusedFactory:IFlowNodeBehaviorFactory
        {
            public bool Supports(string id) { return false; }
            public IFlowNodeBehavior Create(IFlowNode node) { throw new Exception("Data nodes must use their built-in behavior"); }
        }
        [Test] public void MutationAndConditionShareCellsAndRestartResetsInitialValues()
        {
            var registry=NodeDefinitionRegistry.CreateDefault();
            var driver=new VideoFlowDriver(new VideoFlowLoader(registry),new UnusedFactory());
            var nodes=new[]{Node("value","common.number","value",2d),Node("one","common.number","value",1d),
                Node("zero","common.number","value",0d),Node("dec","flow.modify.float32","target","value","operation","subtract"),
                Node("test","flow.condition.float32","operation","greater")};
            var json=Json(registry,nodes,Edge("one","valueOut","dec","operand"),Edge("dec","next","test"),
                Edge("value","valueOut","test","left"),Edge("zero","valueOut","test","right"),Edge("test","true","dec"));
            var loaded=driver.Load(json);Assert.That(loaded.Success,Is.True,loaded.Message);
            Assert.That(driver.Start("dec").Success,Is.True);
            for(int i=0;i<6;i++)driver.Tick(0,0);
            Assert.That(driver.Status,Is.EqualTo(FlowStatus.Completed));
            Assert.That(driver.Values["value"],Is.EqualTo(0f));
            Assert.That(driver.Values["one"],Is.EqualTo(1f));
            driver.Start("test");Assert.That(driver.Values["value"],Is.EqualTo(2f));driver.Stop();
        }
        [Test] public void LoaderRejectsUnsupportedOperationMissingInputAndInvalidTarget()
        {
            var r=NodeDefinitionRegistry.CreateDefault();
            foreach(var target in new[]{"missing","text"})
            {
                var json=Json(r,new[]{Node("text","common.text"),Node("one","common.number","value",1d),
                    Node("write","flow.modify.float32","target",target,"operation","add")},Edge("one","valueOut","write","operand"));
                Assert.That(new VideoFlowLoader(r).Load(json).Success,Is.False);
            }
            Assert.That(new VideoFlowLoader(r).Load(Json(r,new[]{Node("test","flow.condition.bool","operation","less")})).Success,Is.False);
        }
        readonly struct Counter
        {
            public readonly int Count;
            public Counter(int count) { Count=count; }
        }
        [Test] public void CustomStructUsesJsonRepresentationAndRegisteredOperations()
        {
            var r=NodeDefinitionRegistry.CreateDefault();
            r.RegisterDataType("counter",v=>v is IDictionary<string,object> && ((IDictionary<string,object>)v).ContainsKey("count"));
            r.RegisterValueType(new ValueTypeDefinition("counter",v=>v is Counter?v:new Counter(Convert.ToInt32(((IDictionary<string,object>)v)["count"])),
                new Dictionary<string,Func<object,object,object>>{{"merge",(a,b)=>new Counter(((Counter)a).Count+((Counter)b).Count)}},
                new Dictionary<string,Func<object,object,bool>>{{"equal",(a,b)=>((Counter)a).Count==((Counter)b).Count}}));
            r.RegisterValueNodes("custom.counter","计数器","counter",new Dictionary<string,object>{{"count",0}});
            var d=new VideoFlowDriver(new VideoFlowLoader(r),new UnusedFactory());
            var json=Json(r,new[]{Node("a","custom.counter","value",new Dictionary<string,object>{{"count",2}}),
                Node("b","custom.counter","value",new Dictionary<string,object>{{"count",3}}),
                Node("m","flow.modify.counter","target","a","operation","merge"),
                Node("test","flow.condition.counter"),Node("end","common.control")},
                Edge("b","valueOut","m","operand"),Edge("m","next","test"),
                Edge("a","valueOut","test","left"),Edge("b","valueOut","test","right",1),Edge("test","false","end"));
            Assert.That(d.Load(json).Success,Is.True);d.Start("m");
            for(int i=0;i<4;i++)d.Tick(0,0);
            Assert.That(d.Status,Is.EqualTo(FlowStatus.Completed));
            Assert.That(((Counter)d.Values["a"]).Count,Is.EqualTo(5));d.Dispose();
        }
        sealed class ProbeFactory:IFlowNodeBehaviorFactory
        {
            public IFlowNodeContext Context;
            public bool Supports(string type) { return type=="probe"; }
            public IFlowNodeBehavior Create(IFlowNode node) { return new Probe(this); }
            sealed class Probe:IFlowNodeBehavior
            {
                readonly ProbeFactory factory;public Probe(ProbeFactory factory){this.factory=factory;}
                public void Enter(IFlowNodeContext c){factory.Context=c;}
                public void Update(float d,float r){}public void Exit(){}public void Dispose(){}
            }
        }
        [Test] public void CustomBehaviorReadsConnectedCellThroughContextInput()
        {
            var r=NodeDefinitionRegistry.CreateDefault();
            r.Register(new NodeDefinition("probe","probe","test",new[]{new PortDefinition("input","input",PortDirection.Input,PortKind.Data,"float32",1)}));
            var factory=new ProbeFactory();var d=new VideoFlowDriver(new VideoFlowLoader(r),factory);
            var json=Json(r,new[]{Node("value","common.number","value",3d),Node("p","probe")},Edge("value","valueOut","p","input"));
            var loaded=d.Load(json);Assert.That(loaded.Success,Is.True,loaded.Message);
            d.Start("p");object value;Assert.That(factory.Context.TryReadInput("input",out value),Is.True);
            Assert.That(value,Is.EqualTo(3f));d.Dispose();
        }
        [Test] public void StaleActivationCannotReadOrMutateRestartedRun()
        {
            var r=NodeDefinitionRegistry.CreateDefault();r.Register(new NodeDefinition("probe","probe","test",new[]{new PortDefinition("next","next",PortDirection.Output,PortKind.Control)}));
            var factory=new ProbeFactory();var d=new VideoFlowDriver(new VideoFlowLoader(r),factory);
            Assert.That(d.Load(Json(r,new[]{Node("value","common.number","value",2d),Node("p","probe")})).Success,Is.True);
            d.Start("p");var old=factory.Context;Assert.That(old.WriteValue("value",9d),Is.True);d.Stop();d.Start("p");
            Assert.That(old.WriteValue("value",99d),Is.False);Assert.Throws<InvalidOperationException>(()=>old.ReadValue("value"));
            Assert.That(d.Values["value"],Is.EqualTo(2f));
            Assert.That(factory.Context.Complete("next"),Is.True);Assert.That(factory.Context.WriteValue("value",99d),Is.False);d.Dispose();
        }
        [Test] public void IntegerOverflowFaultsWithoutPartiallyChangingValue()
        {
            var r=NodeDefinitionRegistry.CreateDefault();var d=new VideoFlowDriver(new VideoFlowLoader(r),new UnusedFactory());
            var json=Json(r,new[]{Node("a","common.integer","value",int.MaxValue),Node("one","common.integer","value",1),
                Node("m","flow.modify.int32","target","a","operation","add")},Edge("one","valueOut","m","operand"));
            Assert.That(d.Load(json).Success,Is.True);Assert.That(d.Start("m").Success,Is.False);
            Assert.That(d.Status,Is.EqualTo(FlowStatus.Faulted));Assert.That(d.Values["a"],Is.EqualTo(int.MaxValue));
        }
        [Test] public void DataInputWritesOnlyWhenCellReceivesControl()
        {
            var r=NodeDefinitionRegistry.CreateDefault();var d=new VideoFlowDriver(new VideoFlowLoader(r),new UnusedFactory());
            var json=Json(r,new[]{Node("a","common.number","value",7d),Node("b","common.number","value",2d)},Edge("a","valueOut","b","valueIn"));
            Assert.That(d.Load(json).Success,Is.True);d.Start("a");Assert.That(d.Values["b"],Is.EqualTo(2f));d.Tick(0,0);
            Assert.That(d.Status,Is.EqualTo(FlowStatus.Completed));d.Start("b");Assert.That(d.Values["b"],Is.EqualTo(7f));d.Dispose();
        }
    }
}
