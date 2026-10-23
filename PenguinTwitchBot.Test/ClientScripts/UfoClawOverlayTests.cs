using Jint;

namespace PenguinTwitchBot.Test.ClientScripts;

public class UfoClawOverlayTests
{
    private static Engine CreateEngine(string? environment = null)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PenguinTwitchBot.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var engine = new Engine();
        if (environment != null) engine.Execute(environment);
        return engine.Execute(File.ReadAllText(Path.Combine(directory.FullName, "PenguinTwitchBot", "wwwroot", "js", "ufo.js")));
    }

    [Theory]
    [InlineData(1)] [InlineData(8)] [InlineData(40)] [InlineData(200)]
    public void LayoutKeepsLargeObjectsInsideScene(int count)
    {
        var engine = CreateEngine().SetValue("count", count);
        engine.Execute("var pool = UfoClawScene.layout(Array.from({length:count}, (_, index) => ({id:String(index),label:'Prize'})));");
        Assert.True(engine.Evaluate("pool.every(prize => prize.radius >= 68 && prize.x - prize.radius >= 110 && prize.x + prize.radius <= 1810 && prize.y + prize.radius <= 1000)").AsBoolean());
        Assert.Equal(count, engine.Evaluate("new Set(pool.map(prize => prize.id)).size").AsNumber());
    }

    [Fact]
    public void SideGapsConstrainBallsAndRepeatedColorsVaryDeterministically()
    {
        var engine = CreateEngine().Execute("var pool = UfoClawScene.layout(Array.from({length:8}, (_, index) => ({id:String(index),color:'#2ec4b6'})), 1920, 240, 320);");
        Assert.True(engine.Evaluate("pool.every(prize => prize.x - 200 >= 240 && prize.x + 200 <= 1600)").AsBoolean());
        Assert.True(engine.Evaluate("new Set(pool.map(prize => prize.color)).size > 1").AsBoolean());
        Assert.Equal(engine.Evaluate("pool[1].color").AsString(), engine.Evaluate("UfoClawScene.layout([{id:'1',color:'#2ec4b6'},{id:'2',color:'#2ec4b6'}])[1].color").AsString());
    }

    [Fact]
    public void DefaultSoundFilesAreBundledAndReferencedByTheOverlay()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PenguinTwitchBot.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var mediaPath = Path.Combine(directory.FullName, "PenguinTwitchBot", "wwwroot", "media");
        var script = File.ReadAllText(Path.Combine(directory.FullName, "PenguinTwitchBot", "wwwroot", "js", "ufo.js"));
        foreach (var name in new[] { "motor", "grab", "drop", "lift", "catch", "reveal" })
        {
            var file = Path.Combine(mediaPath, $"ufo_claw_{name}.wav");
            var bytes = File.ReadAllBytes(file);
            Assert.True(bytes.Length > 44);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
            Assert.Contains($"/media/ufo_claw_{name}.wav", script);
        }
    }

    [Fact]
    public void SuspenseCanDropCandidatesBeforeReturningToTheFrozenWinner()
    {
        var engine = CreateEngine().Execute("var balls = UfoClawScene.layout(Array.from({length:12}, (_, index) => ({id:String(index),x:250+index*120,y:900}))); var winnerIndex=4; var seed=Array.from({length:100},(_,i)=>'regrab-'+i).find(value=>UfoClawScene.suspenseSequence(value,balls.length,4,winnerIndex).length>1); var sequence = UfoClawScene.suspenseSequence(seed,balls.length,4,winnerIndex).map(index => balls[index]); var winner = balls[winnerIndex]; var frame = seconds => UfoClawScene.frameAt(seconds, winner, true, false, 1, 1920, 0, 0, 100, seed, sequence); var animationEnd=UfoClawScene.animationEndFor(true,false,100,seed,sequence,winner,1920,0,0);");
        var lockTime = engine.Evaluate("frame(0).lockTime").AsNumber();
        var animationEnd = engine.Evaluate("frame(0).animationEnd").AsNumber();
        var firstRegrabTime = engine.Evaluate("(()=>{for(let t=9.25;t<frame(0).timing.feintEnd;t+=0.02)if(frame(t).decoyCaptured)return t;return -1})()").AsNumber();
        Assert.True(engine.Evaluate("sequence.length >= 2 && sequence.length <= 4").AsBoolean());
        Assert.Equal(0, engine.Evaluate("UfoClawScene.suspenseSequence('grab-1', 12, 0).length").AsNumber());
        Assert.True(engine.Evaluate("UfoClawScene.suspenseSequence('grab-1', 12, 5).length <= 5").AsBoolean());
        Assert.True(engine.Evaluate("UfoClawScene.suspenseSequence('grab-1', 12, 4, 4).length >= 1 && UfoClawScene.suspenseSequence('grab-1', 12, 4, 4).length <= 4").AsBoolean());
        Assert.Equal(1, engine.Evaluate("UfoClawScene.suspenseSequence('short-pool', 2, 4, 0).length").AsNumber());
        Assert.Equal(0, engine.Evaluate("UfoClawScene.suspenseSequence('single-entry', 1, 4, 0).length").AsNumber());
        Assert.False(engine.Evaluate("sequence.some(ball => ball.id === winner.id)").AsBoolean());
        Assert.True(engine.Evaluate("UfoClawScene.effectiveDurationFor(20,true,false,100,seed,sequence,winner,1920,0,0)>20").AsBoolean());
        Assert.Equal(engine.Evaluate("sequence.map(ball => ball.id).join(',')").AsString(), engine.Evaluate("UfoClawScene.suspenseSequence(seed, 12, 4, winnerIndex).map(index => balls[index].id).join(',')").AsString());
        Assert.True(engine.Evaluate("frame(7.5).slip && frame(7.5).heldPrize.id === sequence[0].id").AsBoolean());
        Assert.True(engine.Evaluate("frame(7.5).captured && frame(7.5).heldPrize.id === sequence[0].id").AsBoolean());
        Assert.True(firstRegrabTime > 0 && engine.Evaluate($"frame({firstRegrabTime}).decoyCaptured").AsBoolean());
        Assert.True(engine.Evaluate("frame(8.8).decoyY > 500 && frame(8.8).decoyY < 700").AsBoolean());
        Assert.True(engine.Evaluate("frame(9.3).dropped.some(item => item.prize.id === sequence[0].id)").AsBoolean());
        Assert.True(engine.Evaluate("frame(9.3).dropped[0].x === 960 && frame(9.3).dropped[0].y === sequence[0].y").AsBoolean());
        Assert.True(engine.Evaluate("frame(14).dropped.some(item => item.prize.id === sequence[0].id && item.x === 960 && item.y === sequence[0].y)").AsBoolean());
        Assert.True(engine.Evaluate($"!frame({lockTime - 0.001}).locked && !frame({lockTime - 0.001}).reveal").AsBoolean());
        Assert.True(engine.Evaluate($"frame({lockTime}).captured").AsBoolean());
        Assert.Equal(engine.Evaluate("winner.id").AsString(), engine.Evaluate($"frame({lockTime}).heldPrize.id").AsString());
        Assert.Equal(engine.Evaluate("winner.x").AsNumber(), engine.Evaluate($"frame({lockTime}).x").AsNumber());
        Assert.Equal(engine.Evaluate("winner.y - 66").AsNumber(), engine.Evaluate($"frame({lockTime}).clawY").AsNumber());
        Assert.True(engine.Evaluate($"frame({lockTime}).locked && !frame({lockTime}).reveal").AsBoolean());
        Assert.True(engine.Evaluate($"frame({animationEnd}).captured && frame({animationEnd}).heldPrize.id === winner.id && frame({animationEnd}).x === 960 && frame({animationEnd}).clawY === 284 && frame({animationEnd}).reveal").AsBoolean());
        engine.Execute("var noFeint = seconds => UfoClawScene.frameAt(seconds, winner, true, false, 1, 1920, 0, 0, 0, 'grab-1', sequence);");
        Assert.True(engine.Evaluate("!noFeint(8).slip && noFeint(8).heldPrize.id === winner.id").AsBoolean());
        Assert.False(engine.Evaluate("UfoClawScene.shouldSlip('grab-1', 0)").AsBoolean());
        Assert.True(engine.Evaluate("UfoClawScene.shouldSlip('grab-1', 100)").AsBoolean());
    }

    [Fact]
    public void SuspenseMovementIsContinuousAtDropHandoffs()
    {
        var engine = CreateEngine().Execute("var balls = UfoClawScene.layout(Array.from({length:5}, (_, index) => ({id:String(index),x:360+index*280,y:900}))); var winner = balls[2]; var sequence = [balls[0],balls[3],balls[4]]; var frame = seconds => UfoClawScene.frameAt(seconds, winner, true, false, 1, 1920, 0, 0, 100, 'smooth-grab', sequence); var dist = (a,b) => Math.hypot(a.x-b.x,a.clawY-b.clawY);");
        Assert.True(engine.Evaluate("(()=>{const plan=frame(0).timing;const bounds=[plan.feintEnd,plan.searchEnd,plan.winnerApproachEnd,plan.lockTime,plan.animationEnd,...plan.cycles.flatMap(cycle=>[cycle.approachEnd,cycle.catchEnd,cycle.liftEnd,cycle.end])];return bounds.every(time=>dist(frame(time-0.001),frame(time+0.001))<5)})()").AsBoolean());
        engine.Execute("var noExtra = seconds => UfoClawScene.frameAt(seconds, winner, true, false, 1, 1920, 0, 0, 100, 'smooth-grab', [winner]);");
        Assert.True(engine.Evaluate("dist(noExtra(11.449),noExtra(11.451)) < 5").AsBoolean());
    }

    [Fact]
    public void GrabDriftsDuringDescentAndAscentAndRevealsSelectedPrize()
    {
        var engine = CreateEngine().Execute("var prize = {x:1009,y:900}; var frame = seconds => UfoClawScene.frameAt(seconds, prize, false, false);");
        Assert.Equal(960, engine.Evaluate("frame(0).x").AsNumber());
        Assert.True(engine.Evaluate("frame(4).x !== frame(5).x && frame(4).clawY < frame(5).clawY").AsBoolean());
        Assert.True(engine.Evaluate("frame(6).phase === 'Closing' && frame(6.49).phase === 'Closing'").AsBoolean());
        Assert.True(engine.Evaluate("frame(8).x !== frame(9).x && frame(8).clawY > frame(9).clawY").AsBoolean());
        Assert.True(engine.Evaluate("!frame(6).captured && frame(6.5).captured").AsBoolean());
        Assert.False(engine.Evaluate("frame(14.9).reveal").AsBoolean());
        Assert.True(engine.Evaluate("frame(15).reveal").AsBoolean());
        Assert.Equal(960, engine.Evaluate("frame(15).prizeX").AsNumber());
        Assert.Equal(960, engine.Evaluate("frame(15).x").AsNumber());
    }

    [Theory]
    [InlineData("Mystery parcel", 8, "Mystery|parcel")]
    [InlineData("Gift subscription", 12, "Gift|subscription")]
    [InlineData("  Bonus   points  ", 12, "Bonus points")]
    [InlineData("Viewer_123456789", 10, "Viewer_123|456789")]
    [InlineData("Choose the next game", 10, "Choose the|next game")]
    public void LabelsWrapAtWordBoundaries(string label, int width, string expected)
    {
        var engine = CreateEngine().SetValue("label", label).SetValue("width", width);
        Assert.Equal(expected, engine.Evaluate("UfoClawScene.wrapLabel(label, width, 2, text => Array.from(text).length).join('|')").AsString());
    }

    private const string BrowserEnvironment = """
        var result = {hidden:true,textContent:''};
        var paintCalls = 0;
        var paint = new Proxy({}, {get: (_, name) => name === 'measureText' ? text => ({width:text.length*12}) : name.includes('Gradient') ? () => ({addColorStop:()=>{}}) : name === 'clearRect' || name === 'setTransform' ? () => {} : () => {paintCalls++;}});
        var document = {getElementById: id => id === 'result' ? result : {getContext:()=>paint}, addEventListener:()=>{}};
        var URLSearchParams = function() { this.get = name => name === 'key' ? 'test-overlay-key' : null; };
        var matchMedia = () => ({matches:false});
        var location = {search:'?key=test-overlay-key'};
        var performance = {now:()=>0};
        var addEventListener = () => {};
        var nextFrame;
        var requestAnimationFrame = callback => {nextFrame=callback;};
        var WS_TOPICS = {ufoclaw:'ufoclaw'};
        var socket = {send:()=>{}};
        var createWsSocket = () => socket;
        var wsStatusShow = () => {};
        var completionCalls = 0;
        var fetch = url => { if (url === '/api/overlay/ufoclaw-complete') completionCalls++; return new Promise(()=>{}); };
        var snapshot = {type:'ufo_state',revision:1,serverNowUtc:'2026-10-23T00:00:00Z',visible:true,
            entries:[{id:'one',label:'Server Winner',color:'#2ec4b6'}],settings:{durationSeconds:11,revealSeconds:4,autoHide:false,tease:true,soundEnabled:false,volume:0.5},
            grab:{id:'grab-1',startedAtUtc:'2026-10-23T00:00:00Z',winningIndex:0}};
        """;

    [Fact]
    public void SnapshotSelectsWinnerAndStaleUpdatesCannotReplaceIt()
    {
        var engine = CreateEngine(BrowserEnvironment);
        engine.Execute("UfoClawOverlay.acceptSnapshot(snapshot,false); nextFrame(12000); UfoClawOverlay.acceptSnapshot({...snapshot,revision:0,visible:false},true); nextFrame(12000);");
        Assert.True(engine.Evaluate("!result.hidden && result.textContent === 'Server Winner'").AsBoolean());
        engine.Execute("UfoClawOverlay.acceptSnapshot({...snapshot,revision:2,visible:false},true); nextFrame(13000);");
        Assert.True(engine.Evaluate("result.hidden").AsBoolean());
    }

    [Fact]
    public void LateReconnectShowsFrozenResultWithoutRerolling()
    {
        var engine = CreateEngine(BrowserEnvironment);
        engine.Execute("snapshot.serverNowUtc='2026-10-23T00:00:12Z'; UfoClawOverlay.acceptSnapshot(snapshot,false); nextFrame(0);");
        Assert.True(engine.Evaluate("!result.hidden && result.textContent === 'Server Winner'").AsBoolean());
    }

    [Fact]
    public void OverlayAcknowledgesOnlyAfterWinnerRetractsAndRevealAppears()
    {
        var engine = CreateEngine(BrowserEnvironment);
        engine.Execute("UfoClawOverlay.acceptSnapshot(snapshot,true); nextFrame(10265);");
        Assert.Equal(0, engine.Evaluate("completionCalls").AsNumber());
        Assert.True(engine.Evaluate("result.hidden").AsBoolean());
        engine.Execute("nextFrame(11000);");
        Assert.False(engine.Evaluate("result.hidden").AsBoolean());
        Assert.Equal(1, engine.Evaluate("completionCalls").AsNumber());
    }

    [Fact]
    public void HiddenOverlayOnlyDrawsUfoAndPrizePoolAfterExplicitShow()
    {
        var engine = CreateEngine(BrowserEnvironment);
        engine.Execute("UfoClawOverlay.acceptSnapshot({...snapshot,visible:false,grab:null},false); nextFrame(0); var drawsWhileHidden=paintCalls; UfoClawOverlay.acceptSnapshot({...snapshot,revision:2,visible:true,grab:null},true); nextFrame(16);");
        Assert.Equal(0, engine.Evaluate("drawsWhileHidden").AsNumber());
        Assert.True(engine.Evaluate("paintCalls > drawsWhileHidden && result.hidden").AsBoolean());
    }
}