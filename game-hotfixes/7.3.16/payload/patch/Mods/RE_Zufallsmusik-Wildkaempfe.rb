alias old_pbBGMPlay pbBGMPlay

def pbBGMPlay(param, volume = nil, pitch = nil, reset_volume: false)
  name = nil

  if param.is_a?(String)
    name = param
  elsif param.is_a?(RPG::AudioFile)
    name = param.name
  end

  # 🔥 NEU: erkennt alle "Battle- Wild*.ogg"
  if name && name.start_with?("Battle- Wild")
    tracks = ["Battle- Wild.ogg"] + (2..37).map { |i| "Battle- Wild#{i}.ogg" }
    random_track = tracks.sample

    if param.is_a?(String)
      param = random_track
    else
      param = RPG::AudioFile.new(random_track, param.volume, param.pitch)
    end
  end

  old_pbBGMPlay(param, volume, pitch, reset_volume: reset_volume)
end