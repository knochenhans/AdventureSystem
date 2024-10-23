using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using GodotInk;

public partial class Game : Scene
{
	[Signal] public delegate void DialogFinishedEventHandler();

	[Export] public InkStory InkStory { get; set; }
	[Export] PackedScene PlayerCharacterScene { get; set; }

	enum CommandState
	{
		Idle,
		VerbSelected,
		Dialog
	}

	public VariableManager VariableManager { get; set; } = new();
	public ThingManager ThingManager { get; set; } = new();

	public Dictionary<string, string> Verbs { get; set; }
	public Dictionary<string, string> DefaultVerbReactions { get; set; }

	public Interface InterfaceNode { get; set; }
	public Stage StageNode { get; set; }
	public string currentVerbID { get; set; }
	public Camera2D Camera2DNode { get; set; }

	PackedScene IngameMenuScene { get; set; }

	CommandState _currentCommandState = CommandState.Idle;
	private CommandState CurrentCommandState
	{
		get => _currentCommandState;
		set
		{
			if (value == CommandState.Idle)
			{
				InterfaceNode.ResetCommandLabel();
				currentVerbID = "";
			}
			_currentCommandState = value;
		}
	}

	public ScriptManager ScriptManager { get; set; }

	Character CurrentDialogCharacter;

	public Game()
	{

	}

	public override void _Ready()
	{
		base._Ready();

		var cursor = ResourceLoader.Load("res://resources/cursor_64.png");
		Input.SetCustomMouseCursor(cursor, Input.CursorShape.Arrow, new Vector2(29, 29));

		Verbs = new Dictionary<string, string>
		{
			{ "give", "Give" },
			{ "pick_up", "Pick up" },
			{ "use", "Use" },
			{ "open", "Open" },
			{ "look", "Look" },
			{ "push", "Push" },
			{ "close", "Close" },
			{ "talk_to", "Talk to" },
			{ "pull", "Pull" }
		};

		DefaultVerbReactions = new Dictionary<string, string>()
		{
			{ "give", "There’s no one to give anything to." },
			{ "pick_up", "I can’t pick that up." },
			{ "use", "I can’t use that." },
			{ "open", "I can’t open that." },
			{ "look", "I see nothing special." },
			{ "push", "I can’t push that." },
			{ "close", "I can’t close that." },
			{ "talk_to", "There’s no one to talk to." },
			{ "pull", "I can’t pull that." }
		};

		InterfaceNode = GetNode<Interface>("Interface");
		InterfaceNode.Init(Verbs);

		InterfaceNode.GamePanelMouseMotion += _OnGamePanelMouseMotion;
		InterfaceNode.GamePanelMousePressed += _OnGamePanelMousePressed;

		InterfaceNode.ThingClicked += _OnThingClicked;
		InterfaceNode.ThingHovered += _OnThingHovered;

		InterfaceNode.VerbClicked += _OnVerbClicked;
		InterfaceNode.VerbHovered += _OnVerbHovered;
		InterfaceNode.VerbLeave += _OnVerbLeave;

		SwitchStage("Meadow");

		ThingManager.AddThingToIventory += InterfaceNode._OnObjectAddedToInventory;

		Camera2DNode = GetNode<Camera2D>("Camera2D");

		IngameMenuScene = ResourceLoader.Load<PackedScene>("res://addons/GodotAdventureSystem/IngameMenu.tscn");

		ScriptManager = new CustomScriptManager(this);
	}

	private void SwitchStage(string stageID, string entryID = "default")
	{
		StageNode?.QueueFree();

		StageNode = ResourceLoader.Load<PackedScene>($"res://resources/{stageID}.tscn").Instantiate() as Stage;
		AddChild(StageNode);

		StageNode.ThingClicked += _OnThingClicked;
		StageNode.ThingHovered += _OnThingHovered;

		var playerCharacter = PlayerCharacterScene.Instantiate() as PlayerCharacter;
		playerCharacter.SwitchStage += (stageID, entryID) => SwitchStage(stageID, entryID);

		StageNode.InitPlayerCharacter(playerCharacter, entryID);

		ThingManager.Clear();
		ThingManager.RegisterThings(StageNode.CollectThings());
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			switch (keyEvent.Keycode)
			{
				case Key.Escape:
					SceneManagerNode.Quit();
					// SceneManagerNode.ChangeToScene("Menu");
					break;
				case Key.F5:
					var ingameMenu = IngameMenuScene.Instantiate<IngameMenu>();
					AddChild(ingameMenu);
					break;
			}
		}
	}

	public void _OnVerbHovered(string verbID)
	{
		if (CurrentCommandState == CommandState.Idle)
			InterfaceNode.SetCommandLabel(Verbs[verbID]);
	}

	public void _OnVerbLeave()
	{
		if (CurrentCommandState == CommandState.Idle)
			InterfaceNode.ResetCommandLabel();
	}

	public void _OnVerbClicked(string verbID)
	{
		// Logger.Log($"_OnVerbActivated: Verb: {verbID} activated", Logger.LogTypeEnum.Script);

		InterfaceNode.SetCommandLabel(Verbs[verbID]);
		currentVerbID = verbID;
		CurrentCommandState = CommandState.VerbSelected;
	}

	public void _OnGamePanelMouseMotion()
	{
		if (CurrentCommandState == CommandState.Idle)
			InterfaceNode.ResetCommandLabel();
		else if (CurrentCommandState == CommandState.VerbSelected)
			InterfaceNode.SetCommandLabel(Verbs[currentVerbID]);
	}

	public async void _OnGamePanelMousePressed(InputEventMouseButton mouseButtonEvent)
	{
		if (CurrentCommandState == CommandState.Idle)
		{
			await StageNode.PlayerCharacter.MoveTo(mouseButtonEvent.Position / Camera2DNode.Zoom + Camera2DNode.Position, 1);
		}
		else if (CurrentCommandState == CommandState.VerbSelected)
		{
			if (mouseButtonEvent.ButtonIndex == MouseButton.Right)
			{
				CurrentCommandState = CommandState.Idle;
				InterfaceNode.ResetCommandLabel();
			}
		}
	}

	public void _OnThingHovered(string thingID)
	{
		var thing = ThingManager.GetThing(thingID);

		if (thing == null)
		{
			Logger.Log($"_OnThingHovered: Thing {thingID} not registered in ThingManager", Logger.LogTypeEnum.Error);
		}
		else
		{
			// Hovered inventory item
			if (CurrentCommandState == CommandState.Idle)
				InterfaceNode.SetCommandLabel(ThingManager.GetThingName(thingID));
			else if (CurrentCommandState == CommandState.VerbSelected)
				InterfaceNode.SetCommandLabel($"{Verbs[currentVerbID]} {ThingManager.GetThingName(thingID)}");
		}
	}

	public async void _OnThingClicked(string thingID)
	{
		var thing = ThingManager.GetThing(thingID);

		if (thing != null)
		{
			// For objects (that are not in the inventory) and hotspots, move the player character to the object
			if (!ThingManager.IsInInventory(thingID))
			{
				Vector2 position = Vector2.Zero;
				if (thing is Object object_)
					position = object_.Position;
				else if (thing is Character character)
					position = character.Position;
				else if (thing is HotspotArea hotspotArea)
					position = hotspotArea.GetClosestPoint(StageNode.PlayerCharacter.Position) + hotspotArea.Position;
				else
					Logger.Log($"_OnAreaActivated: Area {thing.ID} is not an Object or a HotspotArea", Logger.LogTypeEnum.Error);

				if (position.DistanceTo(StageNode.PlayerCharacter.Position) > 20)
				await StageNode.PlayerCharacter.MoveTo(position, 20);
			}
		}

		if (CurrentCommandState == CommandState.VerbSelected)
		{
			// Interact with the object

			// var result = InkStory.EvaluateFunction("verb", thingID, currentVerbID);

			if (!InkStory.EvaluateFunction("verb", thingID, currentVerbID).AsBool())
			{
				// No scripted reaction found, use the default one
				ScriptManager.ScriptActionQueue.Add(new ScriptActionMessage(StageNode.PlayerCharacter, DefaultVerbReactions[currentVerbID]));
			}
			await ScriptManager.RunActionQueue();

			CurrentCommandState = CommandState.Idle;
			return;
		}
		else
		{
			InkStory.EvaluateFunction("verb", thingID, "walk");
			await ScriptManager.RunActionQueue();
		}

		// Logger.Log($"_OnObjectActivated: Object: {thing.DisplayedName} activated", Logger.LogTypeEnum.Script);

		InterfaceNode.SetCommandLabel(ThingManager.GetThingName(thingID));
		// CurrentCommandState = CommandState.VerbSelected;
	}

	public async Task StartDialog(string characterID)
	{
		Logger.Log($"Starting dialog with {characterID}", Logger.LogTypeEnum.Script);
		InterfaceNode.Mode = Interface.ModeEnum.Dialog;

		CurrentDialogCharacter = ThingManager.GetThing(characterID) as Character;

		StageNode.PlayerCharacter.LookTo(CurrentDialogCharacter.Position);
		StageNode.PlayerCharacter.StartDialog();

		CurrentDialogCharacter.LookTo(StageNode.PlayerCharacter.Position);
		CurrentDialogCharacter.StartDialog();

		InkStory.ChoosePathString(characterID);
		InkStory.Continued += _OnDialogContinue;
		// InkStory.MadeChoice += _OnDialogChoiceMade;
		InterfaceNode.DialogOptionClicked += _OnDialogChoiceMade;
		InkStory.ContinueMaximally();
		// await ToSignal(InkStory, "Continued");
		//TODO: Should this finish only after the dialog is finished? 
		Logger.Log($"Finished dialog with {characterID}", Logger.LogTypeEnum.Script);

		CurrentDialogCharacter.ScriptVisits++;
		// return Task.CompletedTask;
	}

	public async void _OnDialogContinue()
	{
		Logger.Log($"_OnDialogContinue: {InkStory.CurrentText}", Logger.LogTypeEnum.Script);
		if (InkStory.CurrentText.StripEdges() != "")
		{
			var tag = InkStory.GetCurrentTags();

			Character actingCharacter = StageNode.PlayerCharacter;
			Character targetCharacter = CurrentDialogCharacter;

			// First tag defines the currently talking character (and thereby the target character)
			if (tag.Count > 0)
				if (tag[0] != "player")
				{
					actingCharacter = ThingManager.GetThing(tag[0]) as Character;
					targetCharacter = StageNode.PlayerCharacter;
				}

			ScriptManager.ScriptActionQueue.Add(new ScriptActionMessage(actingCharacter, InkStory.CurrentText, targetCharacter));
			ScriptManager.ScriptActionQueue.Add(new ScriptActionCharacterWait(actingCharacter, 0.3f));
		}

		if (InkStory.CanContinue)
			InkStory.Continue();
		else
		{
			await ScriptManager.RunActionQueue();

			if (InkStory.CurrentChoices.Count > 0)
				InterfaceNode.SetDialogChoiceLabels(new Array<InkChoice>(InkStory.CurrentChoices));
			else
			{
				// Story has finished
				InterfaceNode.Mode = Interface.ModeEnum.Normal;
				FinishDialog();
			}
		}
		// CurrentCommandState = CommandState.Idle;
	}

	public async void _OnDialogChoiceMade(InkChoice choice)
	{
		InterfaceNode.ClearDialogChoiceLabels();
		ScriptManager.ScriptActionQueue.Add(new ScriptActionMessage(StageNode.PlayerCharacter, choice.Text, CurrentDialogCharacter));
		ScriptManager.ScriptActionQueue.Add(new ScriptActionCharacterWait(StageNode.PlayerCharacter, 0.5f));

		await ScriptManager.RunActionQueue();

		InkStory.ChooseChoiceIndex(choice.Index);
		InkStory.Continue();
	}

	public void FinishDialog()
	{
		// InkStory.CallDeferred("ResetState");
		InkStory.CallDeferred("ResetCallstack");
		InkStory.Continued -= _OnDialogContinue;

		InterfaceNode.DialogOptionClicked -= _OnDialogChoiceMade;
		InterfaceNode.ClearDialogChoiceLabels();
		InterfaceNode.Mode = Interface.ModeEnum.Normal;

		StageNode.PlayerCharacter.EndDialog();

		CurrentDialogCharacter.EndDialog();
		CurrentDialogCharacter = null;
	}

	public override void _Process(double delta)
	{
		if (StageNode.PlayerCharacter.Position.X > StageNode.GetViewportRect().Size.X / 8)
		{
			if (Camera2DNode.Position.X + StageNode.GetViewportRect().Size.X / 4 < StageNode.GetSize().X)
				Camera2DNode.Position = new Vector2(StageNode.PlayerCharacter.Position.X - StageNode.GetViewportRect().Size.X / 8, Camera2DNode.Position.Y);
		}
		else if (StageNode.PlayerCharacter.Position.X < StageNode.GetViewportRect().Size.X / 8)
		{
			if (Camera2DNode.Position.X - StageNode.GetViewportRect().Size.X / 4 > 0)
				Camera2DNode.Position = new Vector2(StageNode.PlayerCharacter.Position.X - StageNode.GetViewportRect().Size.X / 8, Camera2DNode.Position.Y);
		}
	}
}