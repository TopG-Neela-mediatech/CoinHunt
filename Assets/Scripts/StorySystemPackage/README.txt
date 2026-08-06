STORYSYSTEM — Unity Package
============================

QUICK START (3 steps)
---------------------
1. Drag StoryCanvas_Root.prefab into your Story scene
2. Select StoryCanvas_Root → StoryController → assign your StoryData asset
3. Set sceneToLoadAfterStory in your StoryData → Press Play

WHAT'S INCLUDED
---------------
Scripts/Data/StoryData.cs           - ScriptableObject: slides, sprites, audio
Scripts/Story/StoryController.cs    - Plays slides, handles skip, loads next scene  
Scripts/Story/StoryUI.cs            - Updates images and caption text per slide
Scripts/Story/StoryAnimator.cs      - FadeIn/Out, SlideFromLeft/Right, ScaleUp
Scripts/Core/Events/StoryEvents.cs  - Self-contained event bus
Scripts/Core/Managers/StorySceneLoader.cs - Fade-to-black scene transitions
Prefabs/StoryCanvas_Root.prefab     - Fully wired Canvas (drop into scene and go)
ScriptableObjects/SampleStory.asset - 3-slide sample story to get started

CREATING YOUR OWN STORY
-----------------------
Right-click in Project > Create > StorySystem > Story Data
Fill in slides with background/foreground sprites, caption text, duration
Set sceneToLoadAfterStory to your next scene name

LISTENING TO EVENTS
-------------------
StoryEvents.OnStoryStarted   += () => { };
StoryEvents.OnStoryCompleted += () => { };
StoryEvents.OnStorySkipped   += () => { };
StoryEvents.OnSlideChanged   += (index) => { };

REQUIREMENTS: Unity 2022.3+, TextMeshPro