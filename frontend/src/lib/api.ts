export interface Room {
  hostQueue: Array<RoomMember>;
  session: Session | null;
  members: Record<string, RoomMember>;
  name: string;
  owner: string;
  chat: Chat;
}

export interface RoomMember {
  id: string;
  username: string;
}

export interface Session {
  host: RoomMember;
  startTime: string;
  media: Media;
  likes: number;
  dislikes: number;
}

export interface Media {
  id: string
  url: string
  title: string
  author: Author
  duration: string
  thumbnail?: Thumbnail
}

export interface Author {
  channelId: string
  channelUrl: string
  channelTitle: string
  title: string
}

export interface Thumbnail {
  url: string
  resolution: {
    width: number
    height: number
    area: number
  }
}

export interface Chat {
  messages: ChatMessage[]
}

export interface ChatMessage {
  content: string
  authorID: string
  usernameAtDate: string;
  date: string
}
