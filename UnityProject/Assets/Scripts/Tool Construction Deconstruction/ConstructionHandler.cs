﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine.Networking;

public class ConstructionHandler : NBHandApplyInteractable
{
	public IConstructionHandler RelatedInterface;
	public List<GameObject> ThingsToDrop;
	public List<ConstructionStage> ConstructionStages;
	public SpriteRenderer TSpriteRenderer;
	public RegisterObject registerObject;

	public List<SpriteRenderer> otherSpriteRenderer;
	public Dictionary<int, List<ComponentData>> ContainedObjects = new Dictionary<int, List<ComponentData>>();
	public List<int> ListChance;
	public System.Random random = new System.Random();

	[SyncVar(hook = "ClientGoToStage")]
	public int CurrentStage = 0;
	public bool GenerateComponents = true;

	public GameObject PrefabCircuitBoard;

	[HideInInspector]
	public GameObject CircuitBoard;

	public GameObject StandardConstructionComponent;

	protected override void ServerPerformInteraction(HandApply interaction)
	{
		var slot = InventoryManager.GetSlotFromOriginatorHand(interaction.Performer, interaction.HandSlot.SlotName);

		if (RelatedInterface != null)
		{
			if (!RelatedInterface.InteractionUpdate(interaction, slot, this))
			{
				return;
			}
		}
		Logger.Log("1");
		if (ContainedObjects[CurrentStage] != null)
		{
			Logger.Log("2");
			foreach (var _Object in ContainedObjects[CurrentStage]) {
				Logger.Log("3");				if (_Object.NumberNeeded > _Object.NumberPresent)
				{
					Logger.Log("4");
					if (_Object.GameObject != null)
					{
						Logger.Log("5");
						if (slot.Item.GetComponent(_Object.IdentifyingComponent) != null)
						{
							Logger.Log(_Object.GameObject.GetComponent(_Object.IdentifyingComponent).ToString());
							ConstructionStages[CurrentStage].PresentParts.Add(slot.Item);
							InventoryManager.UpdateInvSlot(true, "", interaction.HandObject, slot.UUID);
							_Object.NumberPresent++;
						}
					}
					else if (_Object.CType != ConstructionElementType.Null){
						var Item = slot.Item?.GetComponent<ConstructionComponent>();
						if (Item != null) {
							if (Item.CType == _Object.CType && Item.level >= _Object.level) {
								ConstructionStages[CurrentStage].PresentParts.Add(slot.Item);
								InventoryManager.UpdateInvSlot(true, "", interaction.HandObject, slot.UUID);
								_Object.NumberPresent++;
							}
						}
					}
				}
			}
		//if 
		}

		var tool = slot.Item?.GetComponent<Tool>();
		//has issues with not spawning them incorrectly
		//if you midway through assembling something then cancel you dont get the items back
		if (tool == null)
		{
			return;
		}
		if (ConstructionStages[CurrentStage].ToolStage.ContainsKey(tool.ToolType))
		{
			var Jump = ConstructionStages[CurrentStage].ToolStage[tool.ToolType];
			if (Jump.ConstructionTime > 0)
			{
				var progressFinishAction = new FinishProgressAction(reason =>
				{
					if (reason == FinishProgressAction.FinishReason.COMPLETED)
					{
						JumpLanding(tool);
					}
				});
				UIManager.ProgressBar.StartProgress(registerObject.WorldPositionServer, Jump.ConstructionTime/tool.SpeedMultiplier, progressFinishAction, interaction.Performer);
			}
			else { 
				JumpLanding(tool);
			}
		}
	}

	public void JumpLanding(Tool tool) { 
		if (ConstructionStages[CurrentStage].ToolStage.ContainsKey(tool.ToolType))
		{
			var Jump = ConstructionStages[CurrentStage].ToolStage[tool.ToolType];
			float SuccessChance = (tool.SuccessChance / 100) * Jump.SuccessChance;
			if (!(SuccessChance < ListChance[random.Next(99)]))
			{
				if (Jump.Construction)
				{
					ConstructionStages[CurrentStage].CheckParts();
					if (!ConstructionStages[CurrentStage].MissingParts)
					{
						GoToStage(Jump.JumpToStage);
					}
				}
				else
				{
					SpawnStage(CurrentStage);
					SpawnStage(Jump.JumpToStage);
					GoToStage(Jump.JumpToStage);
				}
			}
			else {
				Logger.LogError("you Failed!");
			}
		}
	}

	public void ClientGoToStage(int Stage)
	{
		if (CurrentStage == 0 && Stage > 0)
		{
			setOtherSprites(false);

		}
		CurrentStage = Stage;

		Logger.Log(CurrentStage.ToString());
		TSpriteRenderer.sprite = ConstructionStages[CurrentStage].StageSprite;
		if (CurrentStage == 0)
		{
			setOtherSprites(true);
		}
	}

	public void GoToStage(int Stage)
	{
		if (CurrentStage == 0 && Stage > 0)
		{
			gameObject.BroadcastMessage("ObjectStateChange", ObjectState.InConstruction, SendMessageOptions.DontRequireReceiver);
			setOtherSprites(false);

		}
		CurrentStage = Stage;

		Logger.Log(CurrentStage.ToString());
		TSpriteRenderer.sprite = ConstructionStages[CurrentStage].StageSprite;
		if (CurrentStage == 0)
		{
			gameObject.BroadcastMessage("ObjectStateChange", ObjectState.Normal, SendMessageOptions.DontRequireReceiver);
			setOtherSprites(true);
		}
		if (ConstructionStages[CurrentStage].FinalDeconstructedResult != null) {

			SpawnStage(CurrentStage);
			var Objecte = PoolManager.PoolNetworkInstantiate(ConstructionStages[CurrentStage].FinalDeconstructedResult, this.transform.position, parent: this.transform.parent);

			if (CircuitBoard != null)
			{
				CustomNetTransform netTransform = CircuitBoard.GetComponent<CustomNetTransform>();
				netTransform.AppearAtPosition(this.transform.position);
				netTransform.AppearAtPositionServer(this.transform.position);
			}
			Destroy(this.gameObject);
		}
		//ConstructionStages[CurrentStage] 
	}

	public void setOtherSprites(bool Toggle) {
		var sp = this.GetComponentsInChildren<SpriteRenderer>();
		foreach (var SR in sp) {
			if (!otherSpriteRenderer.Contains(SR) && (SR != TSpriteRenderer)) {
				otherSpriteRenderer.Add(SR);
			} 

		}
		//if (prefab != PrefabUtility.GetCorrespondingObjectFromSource(selectedObject))
		foreach (var SR in otherSpriteRenderer) {
			SR.gameObject.SetActive(Toggle);
		}
		TSpriteRenderer.gameObject.SetActive(!Toggle);
	}

	void Start()
	{
		base.Start();
	
		TSpriteRenderer = Instantiate(TSpriteRenderer).GetComponent<SpriteRenderer>();
		TSpriteRenderer.gameObject.transform.SetParent(this.transform, false);
		if (isServer)
		{
			if (RelatedInterface == null)
			{
				RelatedInterface = this.gameObject.GetComponent<IConstructionHandler>();
			}
			if (registerObject == null)
			{
				registerObject = this.gameObject.GetComponent<RegisterObject>();
			}

			if (PrefabCircuitBoard != null && CircuitBoard == null)
			{
				CircuitBoard = PoolManager.PoolNetworkInstantiate(PrefabCircuitBoard, this.transform.position, parent: this.transform.parent);
				if (CircuitBoard != null)
				{
					CustomNetTransform netTransform = CircuitBoard.GetComponent<CustomNetTransform>();
					netTransform.DisappearFromWorldServer();
				}
			}
			ListChance = Enumerable.Range(1, 100).ToList();
			int c = 0;
			foreach (var Stage in ConstructionStages)
			{

				foreach (var TOStageAdvance in Stage.StageAdvances)
				{
					Stage.ToolStage[TOStageAdvance.RequiredTool] = TOStageAdvance;
				}


				GenerateStage(Stage, c);

				if (!ContainedObjects.ContainsKey(c))
				{
					ContainedObjects[c] = new List<ComponentData>();
				}
				c++;

			}

			GoToStage(CurrentStage);
		}
	}

	public void SpawnStage(int StageNumber) { 
		foreach (var _Object in ContainedObjects[StageNumber])
		{
			for (int i = 0; i < ConstructionStages[StageNumber].PresentParts.Count; i++)
			{
				CustomNetTransform netTransform = ConstructionStages[StageNumber].PresentParts[i].GetComponent<CustomNetTransform>();
				netTransform.AppearAtPosition(this.transform.position);
				netTransform.AppearAtPositionServer(this.transform.position);
			}
			_Object.NumberPresent = 0;
		}
		ConstructionStages[StageNumber].PresentParts.Clear();
	}

	public void GenerateStage(ConstructionStage Stage, int StageNumber) {
		if (!ContainedObjects.ContainsKey(StageNumber))
		{
			ContainedObjects[StageNumber] = new List<ComponentData>();
		}
		foreach (var NeededObject in Stage.NeededParts)
		{
			if (Stage.IncludePartsInitialisation && GenerateComponents)
			{
				for (int i = 0; i < NeededObject.NumberNeeded; i++)
				{
					if (NeededObject.GameObject != null)
					{
						var _Object = PoolManager.PoolNetworkInstantiate(NeededObject.GameObject, this.transform.position, parent: this.transform.parent);
						CustomNetTransform netTransform = _Object.GetComponent<CustomNetTransform>();
						netTransform.DisappearFromWorldServer();
						Stage.PresentParts.Add(_Object);
					}
					else if (NeededObject.CType != ConstructionElementType.Null)
					{
						var _Object = PoolManager.PoolNetworkInstantiate(StandardConstructionComponent, this.transform.position, parent: this.transform.parent);
						CustomNetTransform netTransform = _Object.GetComponent<CustomNetTransform>();
						netTransform.DisappearFromWorldServer();
						_Object.GetComponent<ConstructionComponent>().setTypeLevel(NeededObject.CType, NeededObject.level);
						Stage.PresentParts.Add(_Object);
					}
				}
				NeededObject.NumberPresent = NeededObject.NumberNeeded;
			}
			ContainedObjects[StageNumber].Add(NeededObject);
		}
	}

	public override void OnStartClient()
	{
		ClientGoToStage(this.CurrentStage);
		base.OnStartClient();
	}
	private void OnStartServer()
	{
		ClientGoToStage(this.CurrentStage);

		//if extending another component
		base.OnStartServer();
	}
}
