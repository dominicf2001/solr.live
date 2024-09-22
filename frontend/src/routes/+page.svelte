<script lang="ts">
    import "vidstack/bundle";

    $effect(()=>{
        const player = document.querySelector("media-player");
        if (player){
            return player.subscribe(({ paused }) => {
                if (paused){
                    player.play();
                }            
            });
        }
    })

    const videoID = "gPjPb3nNprg";
</script>

<media-player streamType="live" autoPlay muted={true} src="youtube/{videoID}">
  <media-provider></media-provider>
  <media-video-layout></media-video-layout>
</media-player>

<style>
</style>
