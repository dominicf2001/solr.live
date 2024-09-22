export interface Song {
  link: string;
}

export interface SongSession {
  startTime: string;
  song: Song;
  likes: number;
  dislikes: number;
}
