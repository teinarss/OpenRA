
SendMigs = function(waypoints)
	local migEntryPath = { waypoints[1].Location, waypoints[2].Location }
	local migs = Reinforcements.Reinforce(soviets, { "mig" }, migEntryPath, 4)
	Utils.Do(migs, function(mig)
		mig.Move(waypoints[3].Location)
		mig.Move(waypoints[4].Location)
		mig.Destroy()
	end)

	Trigger.AfterDelay(DateTime.Seconds(40), function() SendMigs(waypoints) end)
end

WorldLoaded = function()
	player = Player.GetPlayer("PAKISTAN")
	india = Player.GetPlayer("INDIA")
	
	Trigger.OnObjectiveCompleted(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective completed")
	end)

	Trigger.OnObjectiveFailed(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective failed")
	end)
	
	Trigger.OnKilled(Outpost, OutpostDestroyed)
	
	SurviveObjective = player.AddPrimaryObjective("The outpost must survive.")
	DestroyObjective = india.AddPrimaryObjective("The Pakistani outpost must be destroyed.")
	
	Trigger.AfterDelay(DateTime.Seconds(60), function()
		MissionAccomplished()
		player.MarkCompletedObjective(SurviveObjective)
		india.MarkFailedObjective(DestroyObjective)
	end)
	Trigger.AfterDelay(DateTime.Seconds(2), function() Actor.Create("camera", true, { Owner = player, Location = PakRally.Location }) end)
	Camera.Position = Outpost.CenterPosition
	Trigger.AfterDelay(DateTime.Seconds(5), SendJeeps) --Sending Allied reinforcements	
	SendIndianInfantry(IndiaEntry.Location, Force, ForceInterval) --Sending Indian ground troops every 15 seconds	
	ParadropIndianUnits()
end